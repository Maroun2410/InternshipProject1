using System.ComponentModel.DataAnnotations;
using InternshipProject1.Models;

namespace InternshipProject1.Dtos
{
    public class OwnerDto
    {
        [Required]
        public string Name { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        public string NationalId { get; set; }

        [Required]
        public int Age { get; set; }

        [Required]
        public OwnerType Type { get; set; }
    }
}
