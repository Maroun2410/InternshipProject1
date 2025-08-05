using InternshipProject1.Data;
using InternshipProject1.Models;

namespace OrchardApp.Data.Seed
{
    public static class DbInitializer
    {
        public static void Seed(ApplicationDbContext context)
        {
            if (context.TreeSpecies.Any()) return; // DB already seeded.

            var species = new TreeSpecies[]
            {
                new TreeSpecies { Name = "Cherry", Description = "Sweet red cherries for fresh market", HarvestMonth = "June" },
                new TreeSpecies { Name = "Almond", Description = "Hard-shelled nut tree", HarvestMonth = "September" },
                new TreeSpecies { Name = "Apple", Description = "Crisp apples used for juice and fresh sale", HarvestMonth = "October" },
                new TreeSpecies { Name = "Peach", Description = "Soft summer fruit with fuzzy skin", HarvestMonth = "July" }
            };
            context.TreeSpecies.AddRange(species);
            context.SaveChanges();

            var lands = new Land[]
            {
                new Land { Name = "North Field", AreaInHectares = 3.5, Location = "Zone A1", TreeSpeciesId = 1, TotalTrees = 1200, PlantingDate = DateTime.Parse("2018-03-10") },
                new Land { Name = "East Orchard", AreaInHectares = 2.0, Location = "Zone B2", TreeSpeciesId = 2, TotalTrees = 800, PlantingDate = DateTime.Parse("2019-04-25") },
                new Land { Name = "South Hillside", AreaInHectares = 4.2, Location = "Zone C1", TreeSpeciesId = 3, TotalTrees = 1500, PlantingDate = DateTime.Parse("2020-05-15") },
                new Land { Name = "West Plot", AreaInHectares = 1.8, Location = "Zone D3", TreeSpeciesId = 4, TotalTrees = 600, PlantingDate = DateTime.Parse("2021-02-20") }
            };
            context.Lands.AddRange(lands);
            context.SaveChanges();
        }
    }
}
