namespace Domain.Entities
{
    public class Lease
    {
        public const int TermInMonths = 12;

        public int Id { get; set; }

        public int UnitId { get; set; }
        public Unit Unit { get; set; } = null!;

        public string ApplicantId { get; set; } = string.Empty;
        public ApplicationUser Applicant { get; set; } = null!;

        public int RentalApplicationId { get; set; }
        public RentalApplication RentalApplication { get; set; } = null!;

        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }

        public static Lease ForTwelveMonths(int unitId, string applicantId, int rentalApplicationId, DateOnly startDate) => new()
        {
            UnitId = unitId,
            ApplicantId = applicantId,
            RentalApplicationId = rentalApplicationId,
            StartDate = startDate,
            EndDate = startDate.AddMonths(TermInMonths).AddDays(-1)
        };

        public bool Covers(DateOnly date) => StartDate <= date && date <= EndDate;
    }
}
