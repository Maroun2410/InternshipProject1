using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MobileAPI.Domain;
using MobileAPI.Services;

namespace MobileAPI.Auth;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUser? _current;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser? current = null)
        : base(options) => _current = current;

    // Identity
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Domain
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<Planting> Plantings => Set<Planting>();
    public DbSet<Harvest> Harvests => Set<Harvest>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<WorkerFarmAssignment> WorkerFarmAssignments => Set<WorkerFarmAssignment>();
    public DbSet<WorkerInvite> WorkerInvites => Set<WorkerInvite>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Identity user extras
        b.Entity<ApplicationUser>(u =>
        {
            u.Property(x => x.FullName).HasMaxLength(200);
            u.HasIndex(x => x.Email).IsUnique();
        });

        // Refresh Tokens
        b.Entity<RefreshToken>(rt =>
        {
            rt.ToTable("RefreshTokens");
            rt.HasIndex(x => new { x.UserId, x.TokenHash }).IsUnique();
            rt.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        });

        // Constraints & indexes
        b.Entity<Farm>().HasIndex(x => new { x.OwnerId, x.Name });
        b.Entity<Equipment>().HasIndex(x => new { x.OwnerId, x.Status });
        b.Entity<TaskItem>().HasIndex(x => new { x.OwnerId, x.Status, x.DueDate });
        b.Entity<Planting>().HasIndex(x => new { x.OwnerId, x.FarmId, x.PlantDate });
        b.Entity<Harvest>().HasIndex(x => new { x.OwnerId, x.PlantingId, x.Date });

        b.Entity<WorkerFarmAssignment>(w =>
        {
            w.HasIndex(x => new { x.OwnerId, x.WorkerUserId, x.FarmId }).IsUnique();
        });

        b.Entity<WorkerInvite>(wi =>
        {
            wi.ToTable("WorkerInvites");
            wi.HasIndex(x => new { x.OwnerId, x.Email }).IsUnique();
            wi.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        });

        // ---------- Global query filters ----------
        // IMPORTANT: reference the DbContext instance property (CurrentOwnerId)
        // so EF reads it per DbContext instance — do NOT capture into a local variable.

        b.Entity<Farm>().HasQueryFilter(e =>
            !e.IsDeleted && e.OwnerId == CurrentOwnerId);

        b.Entity<Planting>().HasQueryFilter(e =>
            !e.IsDeleted && e.OwnerId == CurrentOwnerId);

        b.Entity<Harvest>().HasQueryFilter(e =>
            !e.IsDeleted && e.OwnerId == CurrentOwnerId);

        b.Entity<Equipment>().HasQueryFilter(e =>
            !e.IsDeleted && e.OwnerId == CurrentOwnerId);

        b.Entity<TaskItem>().HasQueryFilter(e =>
            !e.IsDeleted && e.OwnerId == CurrentOwnerId);

        // Non-owner-scoped entities still respect soft delete
        b.Entity<WorkerFarmAssignment>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<WorkerInvite>().HasQueryFilter(e => !e.IsDeleted);
        b.Entity<RefreshToken>().HasQueryFilter(e => true); // no soft delete on tokens
    }

    // This is read per DbContext instance at query time (used in HasQueryFilter).
    public Guid CurrentOwnerId => _current?.OwnerId ?? Guid.Empty;

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.IsDeleted = false;

                if (entry.Entity is IOwnerScoped scoped &&
                    scoped.OwnerId == Guid.Empty && CurrentOwnerId != Guid.Empty)
                {
                    scoped.OwnerId = CurrentOwnerId;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
