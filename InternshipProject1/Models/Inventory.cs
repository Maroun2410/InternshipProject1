using System.ComponentModel.DataAnnotations;

namespace InternshipProject1.Models
{
    public enum InventoryStatus
    {
        Available,
        Reserved,
        Sold,
        Damaged
    }

    public class Inventory
    {
        public int Id { get; set; }
        public int HarvestId { get; set; }
        public DateTime StoredDate { get; set; }
        [Range(1, 100000, ErrorMessage = "Quantity must be between 1 and 100,000.")]
        public decimal Quantity { get; set; }
        public string UnitQuantity { get; set; }
        public string Status { get; set; }

        public Harvest Harvest { get; set; }
        public ICollection<Sale> Sales { get; set; }
    }
}
