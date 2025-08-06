using InternshipProject1.Data;
using InternshipProject1.Models;

namespace OrchardApp.Data.Seed
{
    public static class DbInitializer
    {
        public static void Seed(ApplicationDbContext context)
        {
            try
            {
                // Seed Owners
                if (!context.Owners.Any())
                {
                    var owners = new Owner[]
                    {
                        new Owner { Name = "Alice Smith", Email = "alice@example.com", Address = "123 Green Rd", NationalId = "987654321", PhoneNumber = "555-0100", Age = 45, Type = OwnerType.Private },
                        new Owner { Name = "Orchard Co.", Email = "info@orchardco.com", Address = "456 Farm Lane", NationalId = "123456789", PhoneNumber = "555-0200", Age = 12, Type = OwnerType.Public }
                    };
                    context.Owners.AddRange(owners);
                    context.SaveChanges();
                }

                // Seed TreeSpecies
                if (!context.TreeSpecies.Any())
                {
                    var species = new TreeSpecies[]
                    {
                        new TreeSpecies { Name = "Cherry", Description = "Sweet red cherries for fresh market", HarvestMonth = "June" },
                        new TreeSpecies { Name = "Almond", Description = "Hard-shelled nut tree", HarvestMonth = "September" },
                        new TreeSpecies { Name = "Apple", Description = "Crisp apples used for juice and fresh sale", HarvestMonth = "October" },
                        new TreeSpecies { Name = "Peach", Description = "Soft summer fruit with fuzzy skin", HarvestMonth = "July" }
                    };
                    context.TreeSpecies.AddRange(species);
                    context.SaveChanges();
                }

                // Seed Lands with OwnerId
                if (!context.Lands.Any())
                {
                    var speciesList = context.TreeSpecies.ToList();
                    var ownersList = context.Owners.ToList();

                    if (speciesList.Count >= 4 && ownersList.Count >= 2)
                    {
                        var lands = new Land[]
                        {
                            new Land { Name = "North Field", AreaInHectares = 3.5, Location = "Zone A1", TreeSpeciesId = speciesList[0].Id, OwnerId = ownersList[0].Id, TotalTrees = 1200, PlantingDate = DateTime.SpecifyKind(DateTime.Parse("2018-03-10"), DateTimeKind.Utc) },
                            new Land { Name = "East Orchard", AreaInHectares = 2.0, Location = "Zone B2", TreeSpeciesId = speciesList[1].Id, OwnerId = ownersList[1].Id, TotalTrees = 800, PlantingDate = DateTime.SpecifyKind(DateTime.Parse("2019-04-25"), DateTimeKind.Utc) },
                            new Land { Name = "South Hillside", AreaInHectares = 4.2, Location = "Zone C1", TreeSpeciesId = speciesList[2].Id, OwnerId = ownersList[0].Id, TotalTrees = 1500, PlantingDate = DateTime.SpecifyKind(DateTime.Parse("2020-05-15"), DateTimeKind.Utc) },
                            new Land { Name = "West Plot", AreaInHectares = 1.8, Location = "Zone D3", TreeSpeciesId = speciesList[3].Id, OwnerId = ownersList[1].Id, TotalTrees = 600, PlantingDate = DateTime.SpecifyKind(DateTime.Parse("2021-02-20"), DateTimeKind.Utc) }
                        };
                        context.Lands.AddRange(lands);
                        context.SaveChanges();
                    }
                }

                // Seed LandPractices
                if (!context.LandPractices.Any())
                {
                    var landsList = context.Lands.ToList();
                    if (landsList.Count >= 4)
                    {
                        var landPractices = new LandPractices[]
                        {
                            new LandPractices { LandId = landsList[0].Id, WateringMethod = "Drip", FertilizerType = "Organic", FertilizerFreq = "Monthly", PesticideUsed = true, Notes = "Healthy growth" },
                            new LandPractices { LandId = landsList[1].Id, WateringMethod = "Sprinkler", FertilizerType = "Chemical", FertilizerFreq = "Bi-weekly", PesticideUsed = false, Notes = "Pest under control" },
                            new LandPractices { LandId = landsList[2].Id, WateringMethod = "Manual", FertilizerType = "Compost", FertilizerFreq = "Monthly", PesticideUsed = true, Notes = "Dry season" },
                            new LandPractices { LandId = landsList[3].Id, WateringMethod = "Drip", FertilizerType = "Organic", FertilizerFreq = "Weekly", PesticideUsed = false, Notes = "Good yield" }
                        };
                        context.LandPractices.AddRange(landPractices);
                        context.SaveChanges();
                    }
                }

                // Seed Harvests
                if (!context.Harvests.Any())
                {
                    var landsList = context.Lands.ToList();
                    if (landsList.Count >= 4)
                    {
                        var harvests = new Harvest[]
                        {
                            new Harvest { LandId = landsList[0].Id, Date = DateTime.SpecifyKind(DateTime.Parse("2023-06-15"), DateTimeKind.Utc), Quantity = 500, UnitQuantity = "kg", Notes = "Good season" },
                            new Harvest { LandId = landsList[1].Id, Date = DateTime.SpecifyKind(DateTime.Parse("2023-09-20"), DateTimeKind.Utc), Quantity = 350, UnitQuantity = "kg", Notes = "Average" },
                            new Harvest { LandId = landsList[2].Id, Date = DateTime.SpecifyKind(DateTime.Parse("2023-10-05"), DateTimeKind.Utc), Quantity = 700, UnitQuantity = "kg", Notes = "Excellent" },
                            new Harvest { LandId = landsList[3].Id, Date = DateTime.SpecifyKind(DateTime.Parse("2023-07-12"), DateTimeKind.Utc), Quantity = 400, UnitQuantity = "kg", Notes = "Below average" }
                        };
                        context.Harvests.AddRange(harvests);
                        context.SaveChanges();
                    }
                }

                // Seed Inventories
                if (!context.Inventories.Any())
                {
                    var harvestsList = context.Harvests.ToList();
                    if (harvestsList.Count >= 4)
                    {
                        var inventories = new Inventory[]
                        {
                            new Inventory { HarvestId = harvestsList[0].Id, StoredDate = DateTime.UtcNow, Quantity = 200, UnitQuantity = "kg", Status = "Available" },
                            new Inventory { HarvestId = harvestsList[1].Id, StoredDate = DateTime.UtcNow, Quantity = 150, UnitQuantity = "kg", Status = "Sold" },
                            new Inventory { HarvestId = harvestsList[2].Id, StoredDate = DateTime.UtcNow, Quantity = 300, UnitQuantity = "kg", Status = "Available" },
                            new Inventory { HarvestId = harvestsList[3].Id, StoredDate = DateTime.UtcNow, Quantity = 100, UnitQuantity = "kg", Status = "Reserved" }
                        };
                        context.Inventories.AddRange(inventories);
                        context.SaveChanges();
                    }
                }

                // Seed Sales
                if (!context.Sales.Any())
                {
                    var inventoriesList = context.Inventories.ToList();
                    if (inventoriesList.Count >= 4)
                    {
                        var sales = new Sale[]
                        {
                            new Sale { InventoryId = inventoriesList[0].Id, Date = DateTime.UtcNow, Quantity = 50, UnitQuantity = "kg", UnitPrice = 3.5m, BuyerName = "John Doe" },
                            new Sale { InventoryId = inventoriesList[1].Id, Date = DateTime.UtcNow, Quantity = 70, UnitQuantity = "kg", UnitPrice = 2.8m, BuyerName = "FreshMart" },
                            new Sale { InventoryId = inventoriesList[2].Id, Date = DateTime.UtcNow, Quantity = 120, UnitQuantity = "kg", UnitPrice = 4.0m, BuyerName = "FruitCorp" },
                            new Sale { InventoryId = inventoriesList[3].Id, Date = DateTime.UtcNow, Quantity = 30, UnitQuantity = "kg", UnitPrice = 3.0m, BuyerName = "Local Buyer" }
                        };
                        context.Sales.AddRange(sales);
                        context.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Error occurred during database seeding:");
                Console.WriteLine($"Message: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }
    }
}
