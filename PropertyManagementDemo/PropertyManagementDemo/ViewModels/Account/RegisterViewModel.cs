using System.ComponentModel.DataAnnotations;

namespace PropertyManagementDemo.ViewModels.Account
{
    public enum AccountType
    {
        Applicant,
        PropertyManager
    }

    public class RegisterViewModel
    {
        [Required, StringLength(250)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        // Optional, and only collected from applicants.
        [StringLength(250)]
        [Display(Name = "Current address (optional)")]
        public string? CurrentAddress { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "I am a")]
        public AccountType? AccountType { get; set; }
    }
}
