// Dtos/SaleDto.cs
using System.ComponentModel.DataAnnotations;

namespace InternshipProject1.Dtos
{
    public class SaleDto
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public DateTime Date { get; set; }
        [Range(1, 100000, ErrorMessage = "Quantity must be between 1 and 100,000.")]
        public decimal Quantity { get; set; }
        public string UnitQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string BuyerName { get; set; }
    }
}
