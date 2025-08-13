// Middleware/RequestOutcomeMiddleware.cs
using System.Diagnostics;
using InternshipProject1.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace InternshipProject1.Middleware;

public sealed class RequestOutcomeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestOutcomeMiddleware> _logger;

    public RequestOutcomeMiddleware(RequestDelegate next, ILogger<RequestOutcomeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();

            // If ErrorHandlingMiddleware already logged the failure, just SKIP here.
            var errorLogged = context.Items.TryGetValue("__error_logged", out var done) && done is true;

            if (!errorLogged)
            {
                var action = PhraseLog.InferAction(context.Request.Method);
                var entity = PhraseLog.InferEntity(context);
                var outcome = PhraseLog.OutcomeFromStatus(context.Response.StatusCode); // <- correct name
                var where = PhraseLog.WhereFrom(context);
                var corr = PhraseLog.CorrelationIdFrom(context);

                var phrase = PhraseLog.BuildE1(
                    action, entity, outcome,
                    cause: null,
                    key: context.Request.RouteValues.TryGetValue("id", out var id) ? id?.ToString() : null,
                    where: where,
                    correlationId: corr
                );

                _logger.LogInformation("{Phrase} lat={LatencyMs} http={StatusCode}",
                    phrase, sw.ElapsedMilliseconds, context.Response.StatusCode);
            }
        }
    }
}
