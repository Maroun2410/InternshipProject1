using System;
using System.ComponentModel.DataAnnotations;

namespace InternshipProject1.Models.Dtos
{
    public class TreeDto
    {
        public int Id { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public string Name { get; set; } = string.Empty;
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public string Description { get; set; } = string.Empty;
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        [EnumDataType(typeof(TreeStatus), ErrorMessage = "Status must be one of the month sof the year : January, February, March...")]
        public string HarvestMonth { get; set; } = string.Empty;
    }
}
