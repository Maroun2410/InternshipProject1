using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MobileAPI.Services;

namespace MobileAPI.Auth;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public Guid? OwnerId => Guid.Empty;
        public bool IsWorker => false;
        public bool IsOwner => true;
    }

    public AppDbContext CreateDbContext(string[] args)
    {
        var cfg = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = cfg.GetConnectionString("DefaultConnection")
                 ?? "Host=localhost;Port=5432;Database=mobileapi;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cs)
            .Options;

        return new AppDbContext(options, new DesignTimeCurrentUser());
    }
}
