using System.Diagnostics;

namespace InternshipProject1.Middleware
{
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-Id";
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Prefer incoming id if present (e.g., from frontend/gateway)
            var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                                ?? context.TraceIdentifier
                                ?? Activity.Current?.Id
                                ?? Guid.NewGuid().ToString("n");

            context.Items[HeaderName] = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            // Enrich ALL logs in this request with CorrelationId
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            }))
            {
                await _next(context);
            }
        }

        public static string Get(HttpContext ctx) =>
            ctx.Items.TryGetValue(HeaderName, out var v) ? v?.ToString() ?? "" : "";
    }
}
