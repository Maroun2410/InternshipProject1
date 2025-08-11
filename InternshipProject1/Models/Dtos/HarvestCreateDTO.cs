using InternshipProject1;
using System.ComponentModel.DataAnnotations;

public class HarvestCreateDto
{
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
    public int LandId { get; set; }
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
    public DateTime Date { get; set; }
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
    [Range(1, 10000, ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "Quantity must be between 1 and 10,000.")]
    public decimal Quantity { get; set; }
    [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
    public string UnitQuantity { get; set; }
    public string Notes { get; set; }

}
