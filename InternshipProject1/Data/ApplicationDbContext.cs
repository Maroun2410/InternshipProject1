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
    }
}
