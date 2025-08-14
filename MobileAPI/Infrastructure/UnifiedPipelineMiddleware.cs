namespace MobileAPI.Infrastructure;

public class UnifiedPipelineMiddleware
{
    private readonly RequestDelegate _next;
    public UnifiedPipelineMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
    }
}
