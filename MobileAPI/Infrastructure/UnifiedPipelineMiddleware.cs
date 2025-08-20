using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MobileAPI.Infrastructure;

// =====================
// ✅ Success message attribute (optional per action or controller)
// =====================
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class SuccessMessageAttribute : Attribute
{
    public string Message { get; }
    public SuccessMessageAttribute(string message) => Message = message;
}

// =====================
// ✅ Success envelope filter (wraps 200 OK into { message, data })
// =====================
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
