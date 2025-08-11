using System.ComponentModel.DataAnnotations;

namespace InternshipProject1.Dtos
{
    public class LandCreateDto
    {
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public string Name { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        [Range(1, 100, ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "Area must be between 1 and 100.")]
        public double AreaInHectares { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public string Location { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public int TreeSpeciesId { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        [Range(1, 100000, ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "Quantity must be between 1 and 100,000.")]
        public int TotalTrees { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public DateTime PlantingDate { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]

        public Guid OwnerId { get; set; }
    }
}
