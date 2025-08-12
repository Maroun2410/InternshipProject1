using System.Diagnostics;

namespace InternshipProject1.Middleware
{
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-Id";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            // Prefer incoming id if present (e.g., from frontend/gateway)
            var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                                ?? context.TraceIdentifier
                                ?? Activity.Current?.Id
                                ?? Guid.NewGuid().ToString("n");

            context.Items[HeaderName] = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            await _next(context);
        }

        public static string Get(HttpContext ctx) =>
            ctx.Items.TryGetValue(HeaderName, out var v) ? v?.ToString() ?? "" : "";
    }
}
