using Domain.Enums;

namespace Domain.Entities
{
    public class ApplicationStatusHistory
    {
        public int Id { get; set; }

        public int RentalApplicationId { get; set; }
        public RentalApplication RentalApplication { get; set; } = null!;

        public ApplicationStatus? FromStatus { get; set; }
        public ApplicationStatus ToStatus { get; set; }

        public string ChangedById { get; set; } = string.Empty;
        public ApplicationUser ChangedBy { get; set; } = null!;
        public DateTime ChangedAt { get; set; }

        public string? Comment { get; set; }
    }
}
