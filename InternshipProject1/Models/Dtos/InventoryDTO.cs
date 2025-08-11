using InternshipProject1.Models;
using System.ComponentModel.DataAnnotations;

namespace InternshipProject1.DTOs
{
    public class InventoryDto
    {
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public DateTime StoredDate { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        [Range(1, 100000, ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "Quantity must be between 1 and 100,000.")]
        public decimal Quantity { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public string UnitQuantity { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        [EnumDataType(typeof(InventoryStatus), ErrorMessage = "Status must be one of: Available, Reserved, Sold, Damaged.")]
        public string Status { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public int HarvestId { get; set; }  // Foreign Key (Required)
    }
}
