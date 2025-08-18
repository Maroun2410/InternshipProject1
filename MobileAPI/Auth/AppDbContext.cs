using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MobileAPI.Domain;
using MobileAPI.Services;
using System.Linq.Expressions;

namespace MobileAPI.Auth;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUser? _current;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser? current = null)
        : base(options)
    {
        _current = current;
    }

    // Identity-supporting table already present
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Domain
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<Planting> Plantings => Set<Planting>();
    public DbSet<Harvest> Harvests => Set<Harvest>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<WorkerFarmAssignment> WorkerFarmAssignments => Set<WorkerFarmAssignment>();

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

        // Global Owner filter + soft delete for owner-scoped entities
        var ownerId = CurrentOwnerId; // captured once
        foreach (var et in b.Model.GetEntityTypes())
        {
            if (typeof(IOwnerScoped).IsAssignableFrom(et.ClrType))
            {
                var param = Expression.Parameter(et.ClrType, "e");
                var ownerProp = Expression.Property(param, nameof(IOwnerScoped.OwnerId));
                var isDeletedProp = Expression.PropertyOrField(param, nameof(BaseEntity.IsDeleted));

                var ownerEq = Expression.Equal(ownerProp, Expression.Constant(ownerId));
                var notDeleted = Expression.Equal(isDeletedProp, Expression.Constant(false));

                var body = Expression.AndAlso(ownerEq, notDeleted);
                var lambda = Expression.Lambda(body, param);
                et.SetQueryFilter(lambda);
            }
            else if (typeof(BaseEntity).IsAssignableFrom(et.ClrType))
            {
                // For non-owner-scoped entities (like WorkerFarmAssignment), still hide soft-deleted
                var param = Expression.Parameter(et.ClrType, "e");
                var isDeletedProp = Expression.PropertyOrField(param, nameof(BaseEntity.IsDeleted));
                var notDeleted = Expression.Equal(isDeletedProp, Expression.Constant(false));
                var lambda = Expression.Lambda(notDeleted, param);
                et.SetQueryFilter(lambda);
            }
        }
    }

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

                if (entry.Entity is IOwnerScoped scoped && scoped.OwnerId == Guid.Empty && CurrentOwnerId != Guid.Empty)
                    scoped.OwnerId = CurrentOwnerId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
