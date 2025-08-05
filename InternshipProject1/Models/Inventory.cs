namespace InternshipProject1.Models
{
    public class Inventory
    {
        public int Id { get; set; }
        public int HarvestId { get; set; }
        public DateTime StoredDate { get; set; }
        public decimal Quantity { get; set; }
        public string UnitQuantity { get; set; }
        public string Status { get; set; }

        public Harvest Harvest { get; set; }
        public ICollection<Sale> Sales { get; set; }
    }
}
