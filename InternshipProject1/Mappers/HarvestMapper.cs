using InternshipProject1.DTOs;
using InternshipProject1.Models;

namespace InternshipProject1.Mappers
{
    public static class HarvestMapper
    {
        public static HarvestResponseDto MapHarvestToResponseDto(Harvest harvest)
        {
            // This method now maps a Harvest object to a HarvestResponseDto
            // without including any inventory information.
            return new HarvestResponseDto
            {
                Id = harvest.Id,
                LandId = harvest.LandId,
                Date = harvest.Date,
                Quantity = harvest.Quantity,
                UnitQuantity = harvest.UnitQuantity,
                Notes = harvest.Notes,
            };
        }
    }
}