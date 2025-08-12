using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Serilog;

namespace InternshipProject1.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public ErrorHandlingMiddleware(RequestDelegate next, IStringLocalizer<SharedResource> localizer)
        {
            _next = next;
            _localizer = localizer;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Let Swagger/static pass through unwrapped
            if (context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/favicon") ||
                context.Request.Path.StartsWithSegments("/swagger-ui") ||
                context.Request.Path.StartsWithSegments("/index.html"))
            {
                await _next(context);
                return;
            }

            var originalBody = context.Response.Body;
            using var mem = new MemoryStream();
            context.Response.Body = mem;

            try
            {
                await _next(context);

                var actual = context.Response.StatusCode;
                var norm = Normalize(actual);

                // read the original response body (if any)
                mem.Seek(0, SeekOrigin.Begin);
                var payload = await new StreamReader(mem).ReadToEndAsync();

                context.Response.Body = originalBody;
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = norm;

                var correlationId = CorrelationIdMiddleware.Get(context);

                string json;
                if (actual == StatusCodes.Status204NoContent)
                {
                    json = JsonSerializer.Serialize(new
                    {
                        StatusCode = 200,
                        Message = _localizer["NoContent"],
                        CorrelationId = correlationId
                    });
                }
                else if (actual >= 200 && actual < 300)
                {
                    json = JsonSerializer.Serialize(new
                    {
                        StatusCode = 200,
                        Message = _localizer["Success"],
                        CorrelationId = correlationId,
                        Data = TryDeserialize(payload)
                    });
                }
                else if (actual >= 400 && actual < 500)
                {
                    json = JsonSerializer.Serialize(new
                    {
                        StatusCode = 400,
                        Message = GetErrorMessage(actual),
                        CorrelationId = correlationId
                    });
                }
                else if (actual >= 500)
                {
                    json = JsonSerializer.Serialize(new
                    {
                        StatusCode = 500,
                        Message = _localizer["ServerError"],
                        CorrelationId = correlationId
                    });
                }
                else
                {
                    json = JsonSerializer.Serialize(new
                    {
                        StatusCode = actual,
                        Message = _localizer["UnhandledStatus"],
                        CorrelationId = correlationId
                    });
                }

                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                // FULL diagnostics to logs for you
                var correlationId = CorrelationIdMiddleware.Get(context);
                Log.ForContext("CorrelationId", correlationId)
                   .ForContext("Path", context.Request.Path)
                   .ForContext("Method", context.Request.Method)
                   .ForContext("QueryString", context.Request.QueryString.Value)
                   .Error(ex, "Unhandled exception");

                // Friendly message to client
                context.Response.Body = originalBody;
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(new
                {
                    StatusCode = 500,
                    Message = _localizer["ServerError"],
                    CorrelationId = correlationId
                });
                await context.Response.WriteAsync(json);
            }
        }

        private static int Normalize(int status)
        {
            if (status >= 200 && status < 300) return 200;
            if (status >= 400 && status < 500) return 400;
            if (status >= 500) return 500;
            return status;
        }

        private string GetErrorMessage(int status) => status switch
        {
            400 => _localizer["BadRequest"],
            401 => _localizer["Unauthorized"],
            403 => _localizer["Forbidden"],
            404 => _localizer["NotFound"],
            405 => _localizer["MethodNotAllowed"],
            409 => _localizer["Conflict"],
            422 => _localizer["UnprocessableEntity"],
            _ => _localizer["ClientError"]
        };

        private object? TryDeserialize(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try { return JsonSerializer.Deserialize<object>(body); }
            catch { return body; }
        }
    }
}
