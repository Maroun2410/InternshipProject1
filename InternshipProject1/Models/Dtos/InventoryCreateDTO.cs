namespace InternshipProject1.DTOs
{
    public class InventoryCreateDto
    {
        public DateTime StoredDate { get; set; }
        public decimal Quantity { get; set; }
        public string UnitQuantity { get; set; }
        public string Status { get; set; }
    }
}
