using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;

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
            if (context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/favicon") ||
                context.Request.Path.StartsWithSegments("/swagger-ui") ||
                context.Request.Path.StartsWithSegments("/index.html"))
            {
                await _next(context);
                return;
            }

            try
            {
                var originalBodyStream = context.Response.Body;

                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                await _next(context);

                int actualStatusCode = context.Response.StatusCode;
                int normalizedStatusCode = NormalizeStatusCode(actualStatusCode);

                context.Response.Body = originalBodyStream;
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = normalizedStatusCode;

                string jsonResponse;

                if (actualStatusCode == 204)
                {
                    jsonResponse = JsonSerializer.Serialize(new
                    {
                        StatusCode = 200,
                        Message = _localizer["NoContent"]
                    });
                }
                else if (actualStatusCode >= 200 && actualStatusCode < 300)
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    var originalContent = await new StreamReader(memoryStream).ReadToEndAsync();

                    jsonResponse = JsonSerializer.Serialize(new
                    {
                        StatusCode = 200,
                        Message = _localizer["Success"],
                        Data = TryDeserialize(originalContent)
                    });
                }
                else if (actualStatusCode >= 400 && actualStatusCode < 500)
                {
                    jsonResponse = JsonSerializer.Serialize(new
                    {
                        StatusCode = 400,
                        Message = _localizer[GetErrorMessageKey(actualStatusCode)]
                    });
                }
                else if (actualStatusCode >= 500)
                {
                    jsonResponse = JsonSerializer.Serialize(new
                    {
                        StatusCode = 500,
                        Message = _localizer["ServerError"]
                    });
                }
                else
                {
                    jsonResponse = JsonSerializer.Serialize(new
                    {
                        StatusCode = actualStatusCode,
                        Message = _localizer["UnhandledStatus"]
                    });
                }

                await context.Response.WriteAsync(jsonResponse);
            }
            catch (Exception)
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    StatusCode = 500,
                    Message = _localizer["UnexpectedServerError"]
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
            }
        }

        private int NormalizeStatusCode(int statusCode)
        {
            if (statusCode >= 200 && statusCode < 300) return 200;
            if (statusCode >= 400 && statusCode < 500) return 400;
            if (statusCode >= 500) return 500;
            return statusCode;
        }

        private string GetErrorMessageKey(int statusCode)
        {
            return statusCode switch
            {
                400 => "BadRequest",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "NotFound",
                405 => "MethodNotAllowed",
                409 => "Conflict",
                422 => "UnprocessableEntity",
                _ => "ClientError"
            };
        }

        private object TryDeserialize(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return JsonSerializer.Deserialize<object>(json);
            }
            catch
            {
                return json;
            }
        }
    }
}
