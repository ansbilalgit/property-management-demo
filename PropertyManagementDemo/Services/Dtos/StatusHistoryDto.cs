using Domain.Enums;

namespace Services.Dtos
{
    public class StatusHistoryDto
    {
        public int Id { get; set; }
        public ApplicationStatus? FromStatus { get; set; }
        public ApplicationStatus ToStatus { get; set; }
        public string ChangedByName { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
        public string? Comment { get; set; }
    }
}
