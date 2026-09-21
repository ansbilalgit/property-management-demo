namespace Domain.Entities
{
    public class Lease
    {
        public const int TermInMonths = 12;

        private Lease()
        {
        }

        public int Id { get; private set; }

        public int UnitId { get; private set; }
        public Unit Unit { get; private set; } = null!;

        public string ApplicantId { get; private set; } = string.Empty;
        public ApplicationUser Applicant { get; private set; } = null!;

        public int RentalApplicationId { get; private set; }
        public RentalApplication RentalApplication { get; private set; } = null!;

        public DateOnly StartDate { get; private set; }
        public DateOnly EndDate { get; private set; }

        public static Lease ForTwelveMonths(int unitId, string applicantId, int rentalApplicationId, DateOnly startDate) => new()
        {
            UnitId = unitId,
            ApplicantId = applicantId,
            RentalApplicationId = rentalApplicationId,
            StartDate = startDate,
            EndDate = EndOfTerm(startDate)
        };

        // Used when the application has just been approved and may not have a database id yet.
        public static Lease ForTwelveMonths(RentalApplication application, DateOnly startDate) => new()
        {
            UnitId = application.UnitId,
            ApplicantId = application.ApplicantId,
            RentalApplication = application,
            StartDate = startDate,
            EndDate = EndOfTerm(startDate)
        };

        public bool Covers(DateOnly date) => StartDate <= date && date <= EndDate;

        private static DateOnly EndOfTerm(DateOnly startDate) => startDate.AddMonths(TermInMonths).AddDays(-1);
    }
}
