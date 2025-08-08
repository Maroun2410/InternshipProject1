using InternshipProject1.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InternshipProject1.Models
{
    public class Land
    {
        public int Id { get; set; }
        public string Name { get; set; }
        [Range(1, 100, ErrorMessage = "area must be between 1 and 100.")]
        public double AreaInHectares { get; set; }
        public string Location { get; set; }
        public int TreeSpeciesId { get; set; }
        [Range(1, 100000, ErrorMessage = "Quantity must be between 1 and 100,000.")]
        public int TotalTrees { get; set; }
        public DateTime PlantingDate { get; set; }

        public TreeSpecies TreeSpecies { get; set; }
        public ICollection<LandPractices> LandPractices { get; set; }
        public ICollection<Harvest> Harvests { get; set; }
        public Guid OwnerId { get; set; }
        public Owner Owner { get; set; }
    }
}
