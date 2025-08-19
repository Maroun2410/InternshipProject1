using System.Data.Common;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace MobileAPI.Infrastructure;

public class RlsSessionInterceptor : DbConnectionInterceptor
{
    private readonly IHttpContextAccessor _http;

    public RlsSessionInterceptor(IHttpContextAccessor http) => _http = http;

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        // Called after the connection is opened (also on pooled connections).
        if (connection is NpgsqlConnection npg)
        {
            var user = _http.HttpContext?.User;

            var ownerId = user?.FindFirst("owner_id")?.Value;                 // owner (for Owners) or employer owner (for Workers)
            var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;  // current user id
            var isWorker = user?.IsInRole("Worker") == true;

            // Precompute string values (avoid expressions in SQL)
            var ownerTxt = ownerId ?? "00000000-0000-0000-0000-000000000000";
            var workerTxt = userId ?? "00000000-0000-0000-0000-000000000000";
            var isWorkerTxt = isWorker ? "true" : "false";

            await using var cmd = npg.CreateCommand();
            cmd.CommandText = @"
                -- Use set_config(text,text,bool). The 3rd arg 'false' = persist for the session.
                select set_config('app.current_owner',    @owner,   false);
                select set_config('app.worker_user_id',   @worker,  false);
                select set_config('app.is_worker',        @isworker,false);
            ";
            cmd.Parameters.AddWithValue("owner", ownerTxt);
            cmd.Parameters.AddWithValue("worker", workerTxt);
            cmd.Parameters.AddWithValue("isworker", isWorkerTxt);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
