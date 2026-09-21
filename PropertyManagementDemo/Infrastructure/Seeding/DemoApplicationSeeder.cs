using Bogus;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Seeding
{
    public static class DemoApplicationSeeder
    {
        // Applicant and Unit are positions in DemoUserSeeder.ApplicantEmails and in the unit list from DemoPropertySeeder.
        // Every (applicant, unit) pair is unique, which is also the key used to skip applications that already exist.
        private sealed record Spec(
            int Applicant,
            int Unit,
            ApplicationStatus Status,
            int CreatedDaysAgo,
            int Residences,
            bool ResidenceHistorySaved = true,
            string? ReviewComment = null,
            int Manager = 0,
            bool WithdrawnBeforeSubmit = false);

        private static readonly Spec[] Specs =
        [
            // Draft: only the applicant information section saved.
            new(0, 0, ApplicationStatus.Draft, 3, Residences: 0, ResidenceHistorySaved: false),
            // Draft: both sections saved, ready to submit.
            new(1, 1, ApplicationStatus.Draft, 5, Residences: 1),

            new(2, 2, ApplicationStatus.Submitted, 4, Residences: 2),
            new(3, 3, ApplicationStatus.Submitted, 3, Residences: 1),
            new(4, 4, ApplicationStatus.Submitted, 2, Residences: 3),

            new(0, 5, ApplicationStatus.Returned, 12, Residences: 2,
                ReviewComment: "Please add your previous landlord's phone number and resubmit."),

            // Approved: two leases running today, and one that has already expired.
            new(1, 6, ApplicationStatus.Approved, 100, Residences: 2, ReviewComment: "Approved. Welcome!", Manager: 1),
            new(2, 7, ApplicationStatus.Approved, 40, Residences: 1, ReviewComment: "Approved. The lease is ready for signing."),
            new(3, 8, ApplicationStatus.Approved, 600, Residences: 2, ReviewComment: "Approved.", Manager: 1),

            new(4, 9, ApplicationStatus.Denied, 20, Residences: 1,
                ReviewComment: "Income does not meet the requirement for this unit."),
            new(0, 10, ApplicationStatus.Denied, 45, Residences: 2,
                ReviewComment: "Unable to verify residence history.", Manager: 1),

            // Withdrawn after submission, and withdrawn while still a draft.
            new(1, 11, ApplicationStatus.Withdrawn, 15, Residences: 1),
            new(2, 12, ApplicationStatus.Withdrawn, 8, Residences: 0, ResidenceHistorySaved: false, WithdrawnBeforeSubmit: true)
        ];

        // Users come from DemoUserSeeder and units from DemoPropertySeeder. Applications that already exist are skipped.
        public static async Task SeedAsync(AppDbContext context, IReadOnlyList<Unit> units, CancellationToken cancellationToken = default)
        {
            if (units.Count <= Specs.Max(s => s.Unit))
                throw new InvalidOperationException("Not enough seeded units for the demo applications.");

            var faker = new Faker("en_US") { Random = new Randomizer(9012) };
            var now = DateTime.UtcNow;

            var applicants = await LoadUsersAsync(context, DemoUserSeeder.ApplicantEmails, cancellationToken);
            var managers = await LoadUsersAsync(context, DemoUserSeeder.ManagerEmails, cancellationToken);

            foreach (var spec in Specs)
            {
                // Generated before the existence check, so the random sequence is the same on every run.
                var residences = BuildResidences(faker, spec.Residences, DateOnly.FromDateTime(now));

                var applicant = applicants[spec.Applicant];
                var manager = managers[spec.Manager];
                var unit = units[spec.Unit];

                var exists = await context.RentalApplications.AnyAsync(
                    a => a.ApplicantId == applicant.Id && a.UnitId == unit.Id, cancellationToken);
                if (exists)
                    continue;

                var created = now.AddDays(-spec.CreatedDaysAgo);
                var submitted = created.AddDays(1);
                var reviewed = submitted.AddDays(2);
                var wasSubmitted = spec.Status != ApplicationStatus.Draft && !spec.WithdrawnBeforeSubmit;

                var application = new RentalApplication
                {
                    UnitId = unit.Id,
                    ApplicantId = applicant.Id,
                    Status = spec.Status,
                    CreatedAt = created,
                    SubmittedAt = wasSubmitted ? submitted : null,
                    FullName = applicant.FullName,
                    Email = applicant.Email ?? string.Empty,
                    Phone = applicant.PhoneNumber ?? string.Empty,
                    CurrentAddress = applicant.CurrentAddress ?? string.Empty,
                    ApplicantInfoSaved = true,
                    ResidenceHistorySaved = spec.ResidenceHistorySaved
                };

                foreach (var residence in residences)
                    application.Residences.Add(residence);

                AddHistory(application, null, ApplicationStatus.Draft, applicant.Id, created);

                if (wasSubmitted)
                    AddHistory(application, ApplicationStatus.Draft, ApplicationStatus.Submitted, applicant.Id, submitted);

                switch (spec.Status)
                {
                    case ApplicationStatus.Returned:
                    case ApplicationStatus.Approved:
                    case ApplicationStatus.Denied:
                        AddHistory(application, ApplicationStatus.Submitted, spec.Status, manager.Id, reviewed, spec.ReviewComment);
                        break;

                    case ApplicationStatus.Withdrawn when wasSubmitted:
                        AddHistory(application, ApplicationStatus.Submitted, ApplicationStatus.Withdrawn, applicant.Id, reviewed);
                        break;

                    case ApplicationStatus.Withdrawn:
                        AddHistory(application, ApplicationStatus.Draft, ApplicationStatus.Withdrawn, applicant.Id, submitted);
                        break;
                }

                context.RentalApplications.Add(application);
                await context.SaveChangesAsync(cancellationToken);

                if (spec.Status == ApplicationStatus.Approved)
                {
                    // Needs the application id, so it is saved after the application.
                    context.Leases.Add(Lease.ForTwelveMonths(unit.Id, applicant.Id, application.Id, DateOnly.FromDateTime(reviewed)));
                    await context.SaveChangesAsync(cancellationToken);
                }
            }
        }

        private static async Task<List<ApplicationUser>> LoadUsersAsync(
            AppDbContext context, string[] emails, CancellationToken cancellationToken)
        {
            var users = await context.Users.Where(u => emails.Contains(u.Email!)).ToListAsync(cancellationToken);

            return emails
                .Select(email => users.FirstOrDefault(u => u.Email == email)
                    ?? throw new InvalidOperationException($"Seed user {email} is missing. Seed users before applications."))
                .ToList();
        }

        // Most recent residence first; each earlier one ends shortly before the next one started.
        private static List<Residence> BuildResidences(Faker faker, int count, DateOnly today)
        {
            var residences = new List<Residence>();
            var moveOut = today.AddMonths(-faker.Random.Int(1, 3));

            for (var i = 0; i < count; i++)
            {
                var moveIn = moveOut.AddMonths(-faker.Random.Int(12, 36));

                residences.Add(new Residence
                {
                    Address = $"{faker.Address.StreetAddress()}, {faker.Address.City()}, {faker.Address.StateAbbr()} {faker.Address.ZipCode("#####")}",
                    LandlordName = faker.Name.FullName(),
                    LandlordPhone = faker.Phone.PhoneNumber("###-###-####"),
                    MoveInDate = moveIn,
                    MoveOutDate = moveOut
                });

                moveOut = moveIn.AddDays(-faker.Random.Int(0, 30));
            }

            return residences;
        }

        private static void AddHistory(
            RentalApplication application, ApplicationStatus? from, ApplicationStatus to,
            string changedById, DateTime at, string? comment = null)
        {
            application.StatusHistory.Add(new ApplicationStatusHistory
            {
                FromStatus = from,
                ToStatus = to,
                ChangedById = changedById,
                ChangedAt = at,
                Comment = comment
            });
        }
    }
}
