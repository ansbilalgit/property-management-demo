using Domain.Enums;

namespace Infrastructure.Dtos
{
    public class ApplicationDto
    {
        public int Id { get; set; }
        public int UnitId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public ApplicationStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CurrentAddress { get; set; } = string.Empty;
        public bool ApplicantInfoSaved { get; set; }

        public bool ResidenceHistorySaved { get; set; }
        public List<ResidenceDto> Residences { get; set; } = new();
    }
}
