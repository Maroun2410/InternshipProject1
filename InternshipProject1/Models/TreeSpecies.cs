using System.ComponentModel.DataAnnotations;

namespace InternshipProject1.Models
{
    public enum TreeStatus
    {
        January, 
        February, 
        March, 
        April, 
        May, 
        June, 
        July, 
        August, 
        September, 
        October, 
        November, 
        December
    }
    public class TreeSpecies
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        [EnumDataType(typeof(TreeStatus), ErrorMessage = "Status must be one of the month sof the year : January, February, March...")]
        public string HarvestMonth { get; set; }

        public ICollection<Land> Lands { get; set; }
    }
}
