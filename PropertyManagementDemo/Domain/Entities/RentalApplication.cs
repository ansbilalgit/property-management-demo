using Domain.Enums;

namespace Domain.Entities
{
    public class RentalApplication
    {
        public int Id { get; set; }

        public int UnitId { get; set; }
        public Unit Unit { get; set; } = null!;

        public string ApplicantId { get; set; } = string.Empty;
        public ApplicationUser Applicant { get; set; } = null!;

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }

        // Section 1: Applicant Information
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CurrentAddress { get; set; } = string.Empty;
        public bool ApplicantInfoSaved { get; set; }

        // Section 2: Residence History
        public bool ResidenceHistorySaved { get; set; }
        public ICollection<Residence> Residences { get; set; } = new List<Residence>();

        public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();
    }
}
