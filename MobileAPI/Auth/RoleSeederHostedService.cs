using Microsoft.AspNetCore.Identity;

namespace MobileAPI.Auth;

public class RoleSeederHostedService : IHostedService
{
    private readonly IServiceProvider _sp;
    public RoleSeederHostedService(IServiceProvider sp) => _sp = sp;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _sp.CreateScope();
        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var roleName in new[] { "Owner", "Worker" })
        {
            if (!await roleMgr.RoleExistsAsync(roleName))
                await roleMgr.CreateAsync(new IdentityRole<Guid>(roleName));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
