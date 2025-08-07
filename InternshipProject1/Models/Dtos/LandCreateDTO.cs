// Dtos/LandCreateDto.cs
namespace InternshipProject1.Dtos
{
    public class LandCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public double AreaInHectares { get; set; }
        public string Location { get; set; } = string.Empty;
        public int TreeSpeciesId { get; set; }
        public int TotalTrees { get; set; }
        public DateTime PlantingDate { get; set; }
    }
}
