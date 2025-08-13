// Middleware/UnifiedPipelineMiddleware.cs
using System.Diagnostics;
using System.Text.Json;
using InternshipProject1.Logging; // uses your PhraseLog.cs
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace InternshipProject1.Middleware;

public sealed class UnifiedPipelineMiddleware
{
    public const string CorrelationHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<UnifiedPipelineMiddleware> _logger;

    public UnifiedPipelineMiddleware(
        RequestDelegate next,
        IStringLocalizer<SharedResource> localizer,
        ILogger<UnifiedPipelineMiddleware> logger)
    {
        _next = next;
        _localizer = localizer;
        _logger = logger;
    }

    public static string GetCorrelationId(HttpContext ctx) =>
        ctx.Items.TryGetValue(CorrelationHeader, out var v) ? v?.ToString() ?? "" : "";

    public async Task InvokeAsync(HttpContext context)
    {
        // ---- Correlation (first) ----
        var correlationId =
            context.Request.Headers[CorrelationHeader].FirstOrDefault()
            ?? context.TraceIdentifier
            ?? Activity.Current?.Id
            ?? Guid.NewGuid().ToString("n");

        context.Items[CorrelationHeader] = correlationId;
        context.Response.Headers[CorrelationHeader] = correlationId;

        // ---- Let Swagger/static pass through unwrapped ----
        if (context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.StartsWithSegments("/favicon") ||
            context.Request.Path.StartsWithSegments("/swagger-ui") ||
            context.Request.Path.StartsWithSegments("/index.html"))
        {
            await _next(context);
            return;
        }

        // ---- Capture body so we can wrap it uniformly ----
        var originalBody = context.Response.Body;
        await using var mem = new MemoryStream();
        context.Response.Body = mem;

        var sw = Stopwatch.StartNew();
        var errorHandled = false;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Build E1 FAILED phrase
            var action = PhraseLog.InferAction(context.Request.Method);
            var entity = PhraseLog.InferEntity(context);
            var where = PhraseLog.WhereFrom(context);
            var key = context.Request.RouteValues.TryGetValue("id", out var idObj) ? idObj?.ToString() : null;
            var cause = PhraseLog.CauseFromException(ex);

            var phrase = PhraseLog.BuildE1(
                action: action,
                entity: entity,
                outcome: "FAILED",
                cause: cause,
                key: key,
                where: where,
                correlationId: correlationId
            );

            _logger.LogError(ex, "{Phrase}", phrase);

            // Friendly JSON to client
            errorHandled = true;
            context.Response.Body = originalBody;
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var jsonErr = JsonSerializer.Serialize(new
            {
                StatusCode = 500,
                Message = _localizer["ServerError"],
                CorrelationId = correlationId
            });

            await context.Response.WriteAsync(jsonErr);
        }
        finally
        {
            sw.Stop();

            if (!errorHandled)
            {
                // Read original response (if any)
                mem.Seek(0, SeekOrigin.Begin);
                var payload = await new StreamReader(mem).ReadToEndAsync();

                var actual = context.Response.StatusCode;
                var outcome = PhraseLog.OutcomeFromStatus(actual);
                var where = PhraseLog.WhereFrom(context);
                var action = PhraseLog.InferAction(context.Request.Method);
                var entity = PhraseLog.InferEntity(context);

                var phrase = PhraseLog.BuildE1(
                    action: action,
                    entity: entity,
                    outcome: outcome,
                    cause: null,
                    key: context.Request.RouteValues.TryGetValue("id", out var idObj) ? idObj?.ToString() : null,
                    where: where,
                    correlationId: correlationId
                );

                _logger.LogInformation("{Phrase} lat={LatencyMs} http={StatusCode}",
                    phrase, sw.ElapsedMilliseconds, actual);

                // Normalize and wrap response
                context.Response.Body = originalBody;
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = Normalize(actual);

                var json = BuildWrappedJson(
                    actualStatus: actual,
                    correlationId: correlationId,
                    payload: payload
                );

                await context.Response.WriteAsync(json);
            }
        }
    }

    // ----- helpers -----

    private static int Normalize(int status)
    {
        if (status >= 200 && status < 300) return 200;
        if (status >= 400 && status < 500) return 400;
        if (status >= 500) return 500;
        return status;
    }

    private string BuildWrappedJson(int actualStatus, string correlationId, string payload)
    {
        if (actualStatus == StatusCodes.Status204NoContent)
        {
            return JsonSerializer.Serialize(new
            {
                StatusCode = 200,
                Message = _localizer["NoContent"],
                CorrelationId = correlationId
            });
        }

        if (actualStatus >= 200 && actualStatus < 300)
        {
            return JsonSerializer.Serialize(new
            {
                StatusCode = 200,
                Message = _localizer["Success"],
                CorrelationId = correlationId,
                Data = TryDeserialize(payload)
            });
        }

        if (actualStatus >= 400 && actualStatus < 500)
        {
            return JsonSerializer.Serialize(new
            {
                StatusCode = 400,
                Message = GetErrorMessage(actualStatus),
                CorrelationId = correlationId
            });
        }

        if (actualStatus >= 500)
        {
            return JsonSerializer.Serialize(new
            {
                StatusCode = 500,
                Message = _localizer["ServerError"],
                CorrelationId = correlationId
            });
        }

        return JsonSerializer.Serialize(new
        {
            StatusCode = actualStatus,
            Message = _localizer["UnhandledStatus"],
            CorrelationId = correlationId
        });
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

    private static object? TryDeserialize(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try { return JsonSerializer.Deserialize<object>(body); }
        catch { return body; }
    }
}
