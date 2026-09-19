using System.ComponentModel.DataAnnotations;

namespace PropertyManagementDemo.ViewModels.Manager
{
    public class PropertyFormViewModel
    {
        public int? Id { get; set; }

        [Required, StringLength(250)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(250), Display(Name = "Address")]
        public string AddressLine { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string State { get; set; } = string.Empty;

        [Required, StringLength(20), Display(Name = "Postal code")]
        public string PostalCode { get; set; } = string.Empty;
    }
}
