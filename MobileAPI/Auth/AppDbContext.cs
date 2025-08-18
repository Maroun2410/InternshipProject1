using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MobileAPI.Auth;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(u =>
        {
            u.Property(x => x.FullName).HasMaxLength(200);
            u.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<RefreshToken>(rt =>
        {
            rt.ToTable("RefreshTokens"); // ensure table name
            rt.HasIndex(x => new { x.UserId, x.TokenHash }).IsUnique();
            rt.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        });
    }
}
