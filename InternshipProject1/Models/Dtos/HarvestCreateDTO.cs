using System.ComponentModel.DataAnnotations;

public class HarvestCreateDto
{
    public int LandId { get; set; }
    public DateTime Date { get; set; }
    [Range(1, 10000, ErrorMessage = "Quantity must be between 1 and 10,000.")]
    public decimal Quantity { get; set; }
    public string UnitQuantity { get; set; }
    public string Notes { get; set; }

}
