namespace InternshipProject1.DTOs
{
    public class HarvestDTO
    {
        public int LandId { get; set; }
        public DateTime Date { get; set; }
        public decimal Quantity { get; set; }
        public string UnitQuantity { get; set; }
        public string Notes { get; set; }
    }
}
