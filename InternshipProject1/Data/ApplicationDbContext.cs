using Microsoft.EntityFrameworkCore;
using InternshipProject1.Models;

namespace InternshipProject1.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<TreeSpecies> TreeSpecies { get; set; }
        public DbSet<Land> Lands { get; set; }
        public DbSet<LandPractices> LandPractices { get; set; }
        public DbSet<Harvest> Harvests { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<Owner> Owners { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Owner – unique (case-sensitive) at DB level via normal indexes
            modelBuilder.Entity<Owner>()
                .HasIndex(o => o.PhoneNumber)
                .IsUnique()
                .HasDatabaseName("ux_owners_phone");

            modelBuilder.Entity<Owner>()
                .HasIndex(o => o.NationalId)
                .IsUnique()
                .HasDatabaseName("ux_owners_nationalid");

            // NOTE:
            // For TreeSpecies.Name and Owner.Email (case-insensitive uniqueness),
            // we will create functional unique indexes (LOWER(...)) in the EF migration.
            // So we do NOT add IsUnique() here to avoid conflicting indexes.
        }
    }
}
