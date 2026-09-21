using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace PropertyManagementDemo.ViewModels.Applications
{
    public class ReviewFormViewModel
    {
        public int ApplicationId { get; set; }

        [Required(ErrorMessage = "Select an outcome.")]
        public ReviewOutcome? Outcome { get; set; }

        // Required for Return and Deny; that rule is enforced by the service.
        [StringLength(1000)]
        public string? Comment { get; set; }
    }
}
