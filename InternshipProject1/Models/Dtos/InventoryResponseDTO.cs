namespace InternshipProject1.DTOs
{
    public class InventoryResponseDto
    {
        public int Id { get; set; }
        public DateTime StoredDate { get; set; }
        public decimal Quantity { get; set; }
        public string UnitQuantity { get; set; }
        public string Status { get; set; }
        public int HarvestId { get; set; }
    }
}
