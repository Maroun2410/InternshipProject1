namespace InternshipProject1.Dtos
{
    public class LandDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double AreaInHectares { get; set; }
        public string Location { get; set; }
        public int TreeSpeciesId { get; set; }
        public int TotalTrees { get; set; }
        public DateTime PlantingDate { get; set; }
    }
}

