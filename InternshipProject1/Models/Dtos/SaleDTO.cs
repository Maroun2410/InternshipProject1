// Dtos/SaleDto.cs
namespace InternshipProject1.Dtos
{
    public class SaleDto
    {
        public int Id { get; set; }
        public int InventoryId { get; set; }
        public DateTime Date { get; set; }
        public decimal Quantity { get; set; }
        public string UnitQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string BuyerName { get; set; }
    }
}
