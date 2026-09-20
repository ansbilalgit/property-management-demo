using System.ComponentModel.DataAnnotations;

namespace PropertyManagementDemo.ViewModels.Applicant
{
    public class ApplicantInfoSectionViewModel
    {
        [Required, StringLength(250), Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, Phone, StringLength(30)]
        public string Phone { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(250), Display(Name = "Current address")]
        public string CurrentAddress { get; set; } = string.Empty;
    }
}
