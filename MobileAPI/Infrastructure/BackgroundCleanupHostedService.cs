using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MobileAPI.Auth;

namespace MobileAPI.Infrastructure;

/// <summary>
/// Periodically deletes expired/old auth artifacts to keep the DB tidy:
/// - RefreshTokens: expired, or revoked long ago
/// - WorkerInvites: expired long ago, revoked long ago, or accepted long ago
/// </summary>
public sealed class BackgroundCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundCleanupHostedService> _log;
    private readonly CleanupOptions _opts;

    public BackgroundCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundCleanupHostedService> log,
        IOptions<CleanupOptions> opts)
    {
        _scopeFactory = scopeFactory;
        _log = log;
        _opts = opts.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // First run shortly after startup, then at the configured interval
        var delay = TimeSpan.FromMinutes(Math.Max(1, _opts.InitialDelayMinutes));
        var interval = TimeSpan.FromMinutes(Math.Max(5, _opts.IntervalMinutes));

        _log.LogInformation("Cleanup service starting. InitialDelay={Delay} Interval={Interval}", delay, interval);

        try
        {
            await Task.Delay(delay, stoppingToken);
        }
        catch (TaskCanceledException) { }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var now = DateTime.UtcNow;

                // ----- RefreshTokens -----
                // 1) Expired tokens (keep a small grace window before deleting)
                var expiredBefore = now.AddDays(-_opts.RefreshTokensGraceDays);
                var qExpired = db.RefreshTokens
                    .IgnoreQueryFilters()
                    .Where(t => t.ExpiresAt < expiredBefore);

                // 2) Revoked tokens older than retention window
                var revokedBefore = now.AddDays(-_opts.RefreshTokensRevokedKeepDays);
                var qRevoked = db.RefreshTokens
                    .IgnoreQueryFilters()
                    .Where(t => t.RevokedAt != null && t.RevokedAt < revokedBefore);

                var n1 = await SafeExecuteDeleteAsync(qExpired, stoppingToken);
                var n2 = await SafeExecuteDeleteAsync(qRevoked, stoppingToken);

                // ----- WorkerInvites -----
                // Expired/Revoked/Accepted invites older than retention window
                var invitesBefore = now.AddDays(-_opts.InvitesKeepDays);
                var qInv = db.WorkerInvites
                    .IgnoreQueryFilters()
                    .Where(i =>
                        (i.ExpiresAt < invitesBefore) ||
                        (i.IsRevoked && (i.UpdatedAt ?? i.CreatedAt) < invitesBefore) ||
                        (i.AcceptedAt != null && i.AcceptedAt < invitesBefore));

                var n3 = await SafeExecuteDeleteAsync(qInv, stoppingToken);

                _log.LogInformation("Cleanup run completed. RefreshTokens expired={N1}, revoked-old={N2}, invites cleared={N3}", n1, n2, n3);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Cleanup run failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException) { }
        }

        _log.LogInformation("Cleanup service stopping.");
    }

    /// <summary>
    /// Uses ExecuteDeleteAsync when available (EF Core 7+); otherwise falls back to batched delete.
    /// </summary>
    private static async Task<int> SafeExecuteDeleteAsync<TEntity>(IQueryable<TEntity> query, CancellationToken ct) where TEntity : class
    {
#if NET7_0_OR_GREATER
        return await query.ExecuteDeleteAsync(ct);
#else
        int total = 0;
        const int batch = 500;
        while (true)
        {
            var chunk = await query.Take(batch).ToListAsync(ct);
            if (chunk.Count == 0) break;
            query.Provider.Execute<IQueryable<TEntity>>(Expression.Call(
                typeof(Queryable), nameof(Queryable.Where), new[] { typeof(TEntity) },
                query.Expression, Expression.Constant(chunk)));

            chunk.ForEach(e => db.Set<TEntity>().Remove(e));
            total += await db.SaveChangesAsync(ct);
        }
        return total;
#endif
    }
}

/// <summary>
/// Options (bind from Configuration section "Cleanup").
/// </summary>
public sealed class CleanupOptions
{
    /// <summary>Minutes to wait after startup before the first cleanup run. Default 1.</summary>
    public int InitialDelayMinutes { get; set; } = 1;

    /// <summary>Minutes between cleanup runs. Default 30.</summary>
    public int IntervalMinutes { get; set; } = 30;

    /// <summary>Days to keep EXPIRED refresh tokens before hard-deleting. Default 3.</summary>
    public int RefreshTokensGraceDays { get; set; } = 3;

    /// <summary>Days to keep REVOKED refresh tokens before hard-deleting. Default 30.</summary>
    public int RefreshTokensRevokedKeepDays { get; set; } = 30;

    /// <summary>Days to keep invites (expired/revoked/accepted) before hard-deleting. Default 30.</summary>
    public int InvitesKeepDays { get; set; } = 30;
}
