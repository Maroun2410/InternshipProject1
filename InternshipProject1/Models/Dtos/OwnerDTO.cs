using System.ComponentModel.DataAnnotations;
using InternshipProject1.Models;

namespace InternshipProject1.Dtos
{
    public class OwnerDto
    {
        [Required]
        public string Name { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        public string NationalId { get; set; }

        [Required]
        [Range(10, 100, ErrorMessage = "Age must be between 10 and 100.")]
        public int Age { get; set; }

        [Required]
        public OwnerType Type { get; set; }
    }
}
