namespace InternshipProject1.Models
{
    public class Harvest
    {
        public int Id { get; set; }
        public int LandId { get; set; }
        public DateTime Date { get; set; }
        public decimal Quantity { get; set; }
        public string UnitQuantity { get; set; }
        public string Notes { get; set; }

        public Land Lands { get; set; }
        public ICollection<Inventory> Inventories { get; set; }
    }
}
//test1test2test3