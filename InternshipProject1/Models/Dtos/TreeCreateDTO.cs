using System.ComponentModel.DataAnnotations;

namespace InternshipProject1.Models.Dtos
{
    public class TreeCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        [EnumDataType(typeof(TreeStatus), ErrorMessage = "Status must be one of the month sof the year : January, February, March...")]
        public string HarvestMonth { get; set; } = string.Empty;
    }
}
