using System;
using System.Collections.Generic;

namespace InternshipProject1.DTOs
{
    public class HarvestResponseDto
    {
        public int Id { get; set; }
        public int LandId { get; set; }
        public DateTime Date { get; set; }
        public decimal Quantity { get; set; }
        public string UnitQuantity { get; set; }
        public string Notes { get; set; }
    }
}
