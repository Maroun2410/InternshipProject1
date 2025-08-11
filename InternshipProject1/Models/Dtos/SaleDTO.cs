// Dtos/SaleDto.cs
using System.ComponentModel.DataAnnotations;

namespace InternshipProject1.Dtos
{
    public class SaleDto
    {
        public int Id { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public int InventoryId { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public DateTime Date { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        [Range(1, 100000, ErrorMessage = "Quantity must be between 1 and 100,000.")]
        public decimal Quantity { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public string UnitQuantity { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public decimal UnitPrice { get; set; }
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public string BuyerName { get; set; }
    }
}
