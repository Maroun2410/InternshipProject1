using System.ComponentModel.DataAnnotations;
using InternshipProject1.Models;

namespace InternshipProject1.Dtos
{
    public class OwnerDto
    {
        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public string Name { get; set; }

        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        [EmailAddress(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "InvalidEmail")]
        public string Email { get; set; }

        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public string Address { get; set; }

        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        public string NationalId { get; set; }

        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        [Range(10, 100, ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "Validation.QuantityRange")]
        public int Age { get; set; }

        [Required(ErrorMessageResourceType = typeof(SharedResource), ErrorMessageResourceName = "FieldRequired")]
        [EnumDataType(typeof(InventoryStatus), ErrorMessage = "Type must be one of : Private, Public.")]
        public OwnerType Type { get; set; }
    }
}
