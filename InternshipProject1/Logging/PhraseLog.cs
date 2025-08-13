// Logging/PhraseLog.cs
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace InternshipProject1.Logging;

public static class PhraseLog
{
    public const string CorrHeader = "X-Correlation-Id";
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public static string BuildE1(
        string action, string entity, string outcome,
        string? cause, string? key, string where, string correlationId)
    {
        action = action?.Trim() ?? "Call";
        entity = entity?.Trim() ?? "Unknown";
        outcome = outcome?.Trim() ?? "OK";
        cause = TrimValue(cause);
        key = MaskKey(TrimValue(key));
        where = where?.Trim() ?? "N/A";

        var parts = new List<string> { $"{action} {entity} {outcome}" };
        if (!string.IsNullOrEmpty(cause)) parts.Add($"cause={cause}");
        if (!string.IsNullOrEmpty(key)) parts.Add($"key={key}");
        if (!string.IsNullOrEmpty(where)) parts.Add($"where={where}");
        parts.Add($"[Corr:{correlationId}]");

        return string.Join(' ', parts);
    }

    public static string InferAction(string httpMethod) => httpMethod?.ToUpperInvariant() switch
    {
        "GET" => "Get",
        "POST" => "Create",
        "PUT" => "Update",
        "PATCH" => "Patch",
        "DELETE" => "Delete",
        _ => "Call"
    };

    public static string InferEntity(HttpContext ctx)
    {
        // Prefer route pattern, else first path segment
        var route = (ctx.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        var first = (route ?? ctx.Request.Path.Value ?? "/").Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first)) return "Root";
        // orders -> Order
        if (first.EndsWith('s') && first.Length > 1) first = first[..^1];
        return char.ToUpperInvariant(first[0]) + (first.Length > 1 ? first[1..] : "");
    }

    public static string OutcomeFromStatus(int status) => status switch
    {
        >= 500 => "FAILED",
        404 => "NOT_FOUND",
        400 => "INVALID",
        401 => "UNAUTHORIZED",
        403 => "FORBIDDEN",
        409 => "CONFLICT",
        _ => "OK"
    };

    public static string WhereFrom(HttpContext ctx)
    {
        var method = ctx.Request.Method.ToUpperInvariant();
        var route = (ctx.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? ctx.Request.Path.Value ?? "/";
        return $"{method} {route}";
    }

    public static string CorrelationIdFrom(HttpContext ctx)
    => InternshipProject1.Middleware.UnifiedPipelineMiddleware.GetCorrelationId(ctx);


    public static string CauseFromException(Exception ex)
    {
        // Keep it dependency-free. Map common cases by type name or known properties if present.
        var type = ex.GetType().Name;

        // Try read SqlState via reflection for Postgres, if available.
        var sqlState = ex.GetType().GetProperty("SqlState")?.GetValue(ex) as string;
        if (sqlState == "23505") return "UniqueViolation";
        if (sqlState == "23503") return "ForeignKeyViolation";

        if (type.Contains("Timeout", StringComparison.OrdinalIgnoreCase)) return "Timeout";
        if (type.Contains("Concurrency", StringComparison.OrdinalIgnoreCase)) return "Concurrency";
        if (type.Contains("Validation", StringComparison.OrdinalIgnoreCase)) return "Validation";
        return type;
    }

    private static string TrimValue(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return string.Empty;
        var s = v.Trim();
        return s.Length > 32 ? s[..32] + "…" : s;
    }

    private static string MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        var s = key.Trim();
        if (EmailRegex.IsMatch(s))
        {
            var at = s.IndexOf('@');
            return at > 0 ? s[..(at + 1)] + "…" : "…";
        }
        return s.Length > 36 ? s[..36] + "…" : s;
    }
}
