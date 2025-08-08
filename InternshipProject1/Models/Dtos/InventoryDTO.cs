using InternshipProject1.Models;
using System.ComponentModel.DataAnnotations;

namespace InternshipProject1.DTOs
{
    public class InventoryDto
    {
        public DateTime StoredDate { get; set; }
        [Range(1, 100000, ErrorMessage = "Quantity must be between 1 and 100,000.")]
        public decimal Quantity { get; set; }
        public string UnitQuantity { get; set; }
        [EnumDataType(typeof(InventoryStatus), ErrorMessage = "Status must be one of: Available, Reserved, Sold, Damaged.")]
        public string Status { get; set; }
        public int HarvestId { get; set; }  // Foreign Key (Required)
    }
}
