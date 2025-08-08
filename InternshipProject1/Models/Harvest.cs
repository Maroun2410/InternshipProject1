using System.ComponentModel.DataAnnotations;

namespace InternshipProject1.Models
{
    public class Harvest
    {
        public int Id { get; set; }
        public int LandId { get; set; }
        public DateTime Date { get; set; }

        [Range(1, 10000, ErrorMessage = "Quantity must be between 1 and 10,000.")]
        public decimal Quantity { get; set; }
        public string UnitQuantity { get; set; }
        public string Notes { get; set; }

        public Land Lands { get; set; }
    }
}
//test1test2test3