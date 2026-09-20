using System.ComponentModel.DataAnnotations;

namespace PropertyManagementDemo.ViewModels.Applicant
{
    public class ResidenceFormViewModel
    {
        public int? Id { get; set; }

        public int ApplicationId { get; set; }

        [Required, StringLength(250)]
        public string Address { get; set; } = string.Empty;

        [Required, StringLength(250), Display(Name = "Landlord name")]
        public string LandlordName { get; set; } = string.Empty;

        [Required, Phone, StringLength(30), Display(Name = "Landlord phone")]
        public string LandlordPhone { get; set; } = string.Empty;

        [Required, DataType(DataType.Date), Display(Name = "Move-in date")]
        public DateOnly? MoveInDate { get; set; }

        [Required, DataType(DataType.Date), Display(Name = "Move-out date")]
        public DateOnly? MoveOutDate { get; set; }
    }
}
