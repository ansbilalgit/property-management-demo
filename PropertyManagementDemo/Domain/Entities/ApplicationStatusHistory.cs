using Domain.Enums;

namespace Domain.Entities
{
    public class ApplicationStatusHistory
    {
        private ApplicationStatusHistory()
        {
        }

        // Written only by RentalApplication whenever its status changes.
        internal ApplicationStatusHistory(ApplicationStatus? fromStatus, ApplicationStatus toStatus, string changedById, DateTime changedAt, string? comment)
        {
            FromStatus = fromStatus;
            ToStatus = toStatus;
            ChangedById = changedById;
            ChangedAt = changedAt;
            Comment = comment;
        }

        public int Id { get; private set; }

        public int RentalApplicationId { get; private set; }
        public RentalApplication RentalApplication { get; private set; } = null!;

        public ApplicationStatus? FromStatus { get; private set; }
        public ApplicationStatus ToStatus { get; private set; }

        public string ChangedById { get; private set; } = string.Empty;
        public ApplicationUser ChangedBy { get; private set; } = null!;
        public DateTime ChangedAt { get; private set; }

        public string? Comment { get; private set; }
    }
}
