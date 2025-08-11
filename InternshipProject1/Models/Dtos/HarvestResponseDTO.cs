using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InternshipProject1.DTOs
{
    public class HarvestResponseDto
    {
        public int Id { get; set; }
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
}
