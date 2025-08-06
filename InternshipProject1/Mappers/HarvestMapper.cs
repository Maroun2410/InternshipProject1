using InternshipProject1.DTOs;
using InternshipProject1.Models;

namespace InternshipProject1.Mappers
{
    public static class HarvestMapper
    {
        public static HarvestDTO MapHarvestToDTO(Harvest harvest)
        {
            return new HarvestDTO
            {
                LandId = harvest.LandId,
                Date = harvest.Date,
                Quantity = harvest.Quantity,
                UnitQuantity = harvest.UnitQuantity,
                Notes = harvest.Notes,
                Inventories = harvest.Inventories.Select(inventory => new InventoryDto
                {
                    StoredDate = inventory.StoredDate,
                    Quantity = inventory.Quantity,
                    UnitQuantity = inventory.UnitQuantity,
                    Status = inventory.Status,
                    HarvestId = inventory.HarvestId
                }).ToList()
            };
        }
    }
}
