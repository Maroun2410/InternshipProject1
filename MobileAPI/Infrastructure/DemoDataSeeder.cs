using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MobileAPI.Auth;
using MobileAPI.Domain;

// ✅ Alias to avoid conflict with System.Threading.Tasks.TaskStatus
using DomainTaskStatus = MobileAPI.Domain.TaskStatus;

namespace MobileAPI.Infrastructure;

public class DemoDataSeeder : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<DemoDataSeeder> _log;

    public DemoDataSeeder(IServiceProvider sp, ILogger<DemoDataSeeder> log)
    {
        _sp = sp;
        _log = log;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        try
        {
            _log.LogInformation("DemoDataSeeder starting…");

            // Ensure DB & roles
            await db.Database.MigrateAsync(ct);

            if (!await roles.RoleExistsAsync("Owner")) await roles.CreateAsync(new IdentityRole<Guid>("Owner"));
            if (!await roles.RoleExistsAsync("Worker")) await roles.CreateAsync(new IdentityRole<Guid>("Worker"));

            // Owner
            var ownerEmail = "owner@demo.local";
            var owner = await users.FindByEmailAsync(ownerEmail);
            if (owner is null)
            {
                owner = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    Email = ownerEmail,
                    UserName = ownerEmail,
                    FullName = "Demo Owner",
                    EmailConfirmed = true,
                    IsActive = true
                };
                var res = await users.CreateAsync(owner, "Passw0rd!");
                if (!res.Succeeded) throw new Exception("Create owner failed: " + string.Join("; ", res.Errors));
                await users.AddToRoleAsync(owner, "Owner");
            }
            else if (!await users.IsInRoleAsync(owner, "Owner"))
            {
                await users.AddToRoleAsync(owner, "Owner");
            }

            // Ensure this EF session sets RLS variables for the owner
            await db.Database.ExecuteSqlRawAsync(
                "select set_config('app.current_owner', {0}, false);" +
                "select set_config('app.worker_user_id', {1}, false);" +
                "select set_config('app.is_worker', 'false', false);",
                owner.Id.ToString(), owner.Id.ToString());

            // Worker
            var workerEmail = "worker@demo.local";
            var worker = await users.FindByEmailAsync(workerEmail);
            if (worker is null)
            {
                worker = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    Email = workerEmail,
                    UserName = workerEmail,
                    FullName = "Demo Worker",
                    EmailConfirmed = true,
                    IsActive = true,
                    EmployerOwnerId = owner.Id
                };
                var res = await users.CreateAsync(worker, "Passw0rd!");
                if (!res.Succeeded) throw new Exception("Create worker failed: " + string.Join("; ", res.Errors));
                await users.AddToRoleAsync(worker, "Worker");
            }
            else
            {
                if (!await users.IsInRoleAsync(worker, "Worker"))
                    await users.AddToRoleAsync(worker, "Worker");
                if (worker.EmployerOwnerId != owner.Id)
                {
                    worker.EmployerOwnerId = owner.Id;
                    await users.UpdateAsync(worker);
                }
            }

            // Helper for UTC dates
            DateTime Utc(int y, int m, int d) => new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);

            // Farms
            var farmA = await db.Farms.FirstOrDefaultAsync(f => f.OwnerId == owner.Id && f.Name == "Demo Farm A", ct)
                       ?? db.Farms.Add(new Farm { OwnerId = owner.Id, Name = "Demo Farm A", LocationText = "North Field", AreaHa = 12.5m }).Entity;

            var farmB = await db.Farms.FirstOrDefaultAsync(f => f.OwnerId == owner.Id && f.Name == "Demo Farm B", ct)
                       ?? db.Farms.Add(new Farm { OwnerId = owner.Id, Name = "Demo Farm B", LocationText = "South Field", AreaHa = 8.0m }).Entity;

            await db.SaveChangesAsync(ct);

            // Assignment (worker -> farmA)
            var assign = await db.WorkerFarmAssignments
                .FirstOrDefaultAsync(a => a.OwnerId == owner.Id && a.WorkerUserId == worker.Id && a.FarmId == farmA.Id, ct);
            if (assign is null)
            {
                db.WorkerFarmAssignments.Add(new WorkerFarmAssignment
                {
                    OwnerId = owner.Id,
                    WorkerUserId = worker.Id,
                    FarmId = farmA.Id
                });
                await db.SaveChangesAsync(ct);
            }

            // Equipment
            if (!await db.Equipment.AnyAsync(e => e.OwnerId == owner.Id, ct))
            {
                db.Equipment.AddRange(
                    new Equipment { OwnerId = owner.Id, Name = "Tractor A", Status = EquipmentStatus.Available, FarmId = farmA.Id },
                    new Equipment { OwnerId = owner.Id, Name = "Harvester H1", Status = EquipmentStatus.Maintenance, FarmId = farmA.Id },
                    new Equipment { OwnerId = owner.Id, Name = "Irrigation Pump", Status = EquipmentStatus.InUse, FarmId = farmB.Id },
                    new Equipment { OwnerId = owner.Id, Name = "Pickup Truck", Status = EquipmentStatus.Available, FarmId = null } // owner-wide
                );
                await db.SaveChangesAsync(ct);
            }

            // Plantings + Harvest
            if (!await db.Plantings.AnyAsync(p => p.OwnerId == owner.Id, ct))
            {
                var p1 = new Planting
                {
                    OwnerId = owner.Id,
                    FarmId = farmA.Id,
                    CropName = "Wheat",
                    PlantDate = Utc(DateTime.UtcNow.Year, 3, 10),
                    ExpectedHarvestDate = Utc(DateTime.UtcNow.Year, 7, 1)
                };
                var p2 = new Planting
                {
                    OwnerId = owner.Id,
                    FarmId = farmB.Id,
                    CropName = "Corn",
                    PlantDate = Utc(DateTime.UtcNow.Year, 4, 5),
                    ExpectedHarvestDate = Utc(DateTime.UtcNow.Year, 8, 10)
                };
                db.Plantings.AddRange(p1, p2);
                await db.SaveChangesAsync(ct);

                db.Harvests.Add(new Harvest
                {
                    OwnerId = owner.Id,
                    PlantingId = p1.Id,
                    Date = Utc(DateTime.UtcNow.Year, 7, 6),
                    QuantityKg = 1240.5m,
                    Notes = "Good yield"
                });
                await db.SaveChangesAsync(ct);
            }

            // Tasks (✅ use alias DomainTaskStatus)
            if (!await db.Tasks.AnyAsync(t => t.OwnerId == owner.Id, ct))
            {
                db.Tasks.AddRange(
                    new TaskItem
                    {
                        OwnerId = owner.Id,
                        Title = "Irrigation valve check",
                        Description = "Inspect valves on north line",
                        FarmId = farmA.Id,
                        DueDate = Utc(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day).AddDays(2),
                        Status = DomainTaskStatus.Todo
                    },
                    new TaskItem
                    {
                        OwnerId = owner.Id,
                        Title = "Tractor A maintenance",
                        Description = "Oil & filter",
                        FarmId = farmA.Id,
                        DueDate = Utc(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day).AddDays(5),
                        Status = DomainTaskStatus.InProgress
                    },
                    new TaskItem
                    {
                        OwnerId = owner.Id,
                        Title = "Inventory count",
                        Description = "Owner-wide task (no farm)",
                        FarmId = null,
                        DueDate = null,
                        Status = DomainTaskStatus.Todo
                    }
                );
                await db.SaveChangesAsync(ct);
            }

            _log.LogInformation("Demo data ready. Owner: {Owner} / Worker: {Worker} (passwords: Passw0rd!)", ownerEmail, workerEmail);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "DemoDataSeeder failed.");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
