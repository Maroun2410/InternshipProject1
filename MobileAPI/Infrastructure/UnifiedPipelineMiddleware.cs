using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Authorization;

namespace MobileAPI.Infrastructure;

// Success message attribute (optional per action or controller)
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SuccessMessageAttribute : Attribute
{
    public string Message { get; }
    public SuccessMessageAttribute(string message) => Message = message;
}

// Success envelope filter (wraps 200 OK into { message, data })
public sealed class SuccessEnvelopeFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Only touch MVC controller results (this filter runs only for controllers)
        switch (context.Result)
        {
            case ObjectResult objRes:
                {
                    var status = objRes.StatusCode ?? 200;
                    var isProblem = objRes.Value is ProblemDetails;
                    if (status == 200 && !isProblem && objRes.Value is not FileResult)
                    {
                        var message = ResolveMessage(context) ?? "OK";

                        if (objRes.Value is null)
                        {
                            context.Result = new OkObjectResult(new { message });
                        }
                        else
                        {
                            var hasMessageProp = objRes.Value.GetType()
                                .GetProperty("message", BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance) != null;

                            if (!hasMessageProp)
                            {
                                context.Result = new OkObjectResult(new { message, data = objRes.Value });
                            }
                            // else: already has a message -> leave as-is
                        }
                    }
                    break;
                }
            case OkResult:
                {
                    var message = ResolveMessage(context) ?? "OK";
                    context.Result = new OkObjectResult(new { message });
                    break;
                }
            default:
                // leave non-Object 200s (e.g., FileResult) and non-200s untouched
                break;
        }

        await next();
    }

    private static string? ResolveMessage(ResultExecutingContext ctx)
    {
        // Prefer action/class attribute via ActionDescriptor metadata
        var fromAction = ctx.ActionDescriptor?.EndpointMetadata?.OfType<SuccessMessageAttribute>()?.FirstOrDefault();
        if (fromAction != null) return fromAction.Message;

        // Fallback to endpoint metadata (should be same, but double-check)
        var fromEndpoint = ctx.HttpContext.GetEndpoint()?.Metadata.GetMetadata<SuccessMessageAttribute>();
        return fromEndpoint?.Message;
    }
}

// =====================
// ✅ Unified exception -> ProblemDetails middleware (unchanged behavior)
// =====================
public class UnifiedPipelineMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UnifiedPipelineMiddleware> _log;
    private readonly IWebHostEnvironment _env;

    public UnifiedPipelineMiddleware(RequestDelegate next, ILogger<UnifiedPipelineMiddleware> log, IWebHostEnvironment env)
    {
        _next = next;
        _log = log;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

            var status = ex switch
            {
                BadHttpRequestException => StatusCodes.Status400BadRequest,
                UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status500InternalServerError
            };

            var problem = new ProblemDetails
            {
                Type = "about:blank",
                Title = status == 500 ? "An unexpected error occurred." : ex.Message,
                Status = status,
                Detail = _env.IsDevelopment() ? ex.ToString() : null,
                Instance = ctx.Request.Path
            };
            problem.Extensions["traceId"] = ctx.TraceIdentifier;

            ctx.Response.Clear();
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/problem+json";

            var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
            await ctx.Response.WriteAsync(json);
        }
    }
}

public sealed class StandardResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Always document Problem Details errors
        AddOrSet(operation, "400", "Bad Request (validation failed)");
        AddOrSet(operation, "404", "Not Found");
        AddOrSet(operation, "500", "Server Error");

        var hasAuthorize = context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any()
                           || context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any() == true;
        var hasAllowAnon = context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any()
                           || context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any() == true;

        if (hasAuthorize && !hasAllowAnon)
        {
            AddOrSet(operation, "401", "Unauthorized");
            AddOrSet(operation, "403", "Forbidden");
        }

        // Standard 200 description to reflect your { message, data } envelope
        if (!operation.Responses.ContainsKey("200"))
        {
            operation.Responses["200"] = new OpenApiResponse { Description = "OK (message + data envelope)" };
        }
        else
        {
            operation.Responses["200"].Description = "OK (message + data envelope)";
        }
    }

    private static void AddOrSet(OpenApiOperation op, string code, string desc)
    {
        if (op.Responses.ContainsKey(code)) op.Responses[code].Description = desc;
        else op.Responses[code] = new OpenApiResponse { Description = desc };
    }
}

public sealed class ExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext ctx)
    {
        var t = ctx.Type;

        // Only add examples to request DTOs you use often (strings keep it simple)
        schema.Example = t.FullName switch
        {
            "MobileAPI.Auth.LoginRequest" => Obj(
                ("email", "owner@example.com"),
                ("password", "P@ssw0rd!"),
                ("device", "Android"),
                ("userAgent", "MobileApp/1.0 (Pixel 8; Android 14)")
            ),

            "MobileAPI.Auth.RegisterOwnerRequest" => Obj(
                ("email", "owner@example.com"),
                ("password", "P@ssw0rd!"),
                ("fullName", "Olive Farmer")
            ),

            "MobileAPI.Auth.RefreshRequest" => Obj(
                ("refreshToken", "paste-refresh-token-here"),
                ("device", "Android"),
                ("userAgent", "MobileApp/1.0 (Pixel 8; Android 14)")
            ),

            "MobileAPI.Auth.LogoutRequest" => Obj(
                ("refreshToken", "paste-refresh-token-here")
            ),

            "MobileAPI.Auth.ForgotPasswordRequest" => Obj(
                ("email", "owner@example.com")
            ),

            "MobileAPI.Auth.ResetPasswordRequest" => Obj(
                ("userId", "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                ("token", "paste-reset-token-here"),
                ("newPassword", "NewP@ssw0rd!")
            ),

            "MobileAPI.Workers.InviteWorkerRequest" => Obj(
                ("email", "worker@example.com"),
                ("fullName", "Field Worker"),
                ("expiresDays", "7")
            ),

            "MobileAPI.Workers.AcceptInviteRequest" => Obj(
                ("email", "worker@example.com"),
                ("token", "paste-invite-token-here"),
                ("password", "P@ssw0rd!"),
                ("fullName", "Field Worker")
            ),

            _ => schema.Example // leave unchanged
        };
    }

    private static IOpenApiAny Obj(params (string key, string value)[] kvps)
    {
        var o = new OpenApiObject();
        foreach (var (k, v) in kvps) o[k] = new OpenApiString(v);
        return o;
    }
}