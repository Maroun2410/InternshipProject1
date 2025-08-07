namespace InternshipProject1.Dtos
{
    public class LandCreateDto
    {
        public string Name { get; set; }
        public double AreaInHectares { get; set; }
        public string Location { get; set; }
        public int TreeSpeciesId { get; set; }
        public int TotalTrees { get; set; }
        public DateTime PlantingDate { get; set; }

        public Guid OwnerId { get; set; } // <-- Add this line
    }
}
