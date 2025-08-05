namespace InternshipProject1.Models
{
    public class Land
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double AreaInHectares { get; set; }
        public string Location { get; set; }
        public int TreeSpeciesId { get; set; }
        public int TotalTrees { get; set; }
        public DateTime PlantingDate { get; set; }

        public TreeSpecies TreeSpecies { get; set; }
        public ICollection<LandPractices> LandPractices { get; set; }
        public ICollection<Harvest> Harvests { get; set; }
    }
}
