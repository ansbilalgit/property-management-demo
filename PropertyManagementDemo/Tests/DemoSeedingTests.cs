using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Tests
{
    public class DemoSeedingTests
    {
        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private static void AddUnitTypes(AppDbContext db)
        {
            db.UnitTypes.AddRange(
                new UnitType { Name = "Studio", IsActive = true },
                new UnitType { Name = "Apartment", IsActive = true },
                new UnitType { Name = "Townhouse", IsActive = true },
                new UnitType { Name = "Loft", IsActive = false });
            db.SaveChanges();
        }

        // DemoUserSeeder needs a real UserManager, so the tests add the same accounts directly.
        private static void AddUsers(AppDbContext db)
        {
            foreach (var email in DemoUserSeeder.ManagerEmails.Concat(DemoUserSeeder.ApplicantEmails))
            {
                db.Users.Add(new ApplicationUser
                {
                    Id = email,
                    UserName = email,
                    Email = email,
                    FullName = $"Demo {email}",
                    PhoneNumber = "555-0100",
                    CurrentAddress = "1 Demo St"
                });
            }

            db.SaveChanges();
        }

        private static async Task SeedAllAsync(AppDbContext db)
        {
            var units = await DemoPropertySeeder.SeedAsync(db);
            await DemoApplicationSeeder.SeedAsync(db, units);
        }

        private static AppDbContext CreateSeededContext()
        {
            var db = CreateContext();
            AddUnitTypes(db);
            AddUsers(db);
            return db;
        }

        [Fact]
        public async Task Seeding_creates_properties_units_and_applications_in_every_status()
        {
            using var db = CreateSeededContext();

            await SeedAllAsync(db);

            Assert.Equal(4, await db.Properties.CountAsync());
            Assert.Equal(20, await db.Units.CountAsync());
            Assert.Equal(13, await db.RentalApplications.CountAsync());

            var statuses = await db.RentalApplications.Select(a => a.Status).Distinct().ToListAsync();
            foreach (var status in Enum.GetValues<ApplicationStatus>())
                Assert.Contains(status, statuses);
        }

        [Fact]
        public async Task Seeding_twice_adds_nothing()
        {
            using var db = CreateSeededContext();
            await SeedAllAsync(db);
            var counts = await CountsAsync(db);

            await SeedAllAsync(db);

            Assert.Equal(counts, await CountsAsync(db));
        }

        [Fact]
        public async Task Seeding_after_a_partial_run_only_fills_in_what_is_missing()
        {
            using var db = CreateSeededContext();
            await SeedAllAsync(db);
            var counts = await CountsAsync(db);

            var removed = await db.RentalApplications.OrderBy(a => a.Id).FirstAsync(a => a.Status == ApplicationStatus.Submitted);
            db.RentalApplications.Remove(removed);
            await db.SaveChangesAsync();

            await SeedAllAsync(db);

            Assert.Equal(counts, await CountsAsync(db));
        }

        [Fact]
        public async Task Every_approved_application_has_exactly_one_lease_and_no_other_status_has_one()
        {
            using var db = CreateSeededContext();
            await SeedAllAsync(db);

            var approvedIds = await db.RentalApplications
                .Where(a => a.Status == ApplicationStatus.Approved).Select(a => a.Id).ToListAsync();
            var leaseApplicationIds = await db.Leases.Select(l => l.RentalApplicationId).ToListAsync();

            Assert.Equal(3, approvedIds.Count);
            Assert.Equal(approvedIds.Order(), leaseApplicationIds.Order());
        }

        [Fact]
        public async Task Two_leases_are_active_today_and_one_has_expired()
        {
            using var db = CreateSeededContext();
            await SeedAllAsync(db);
            var today = DateOnly.FromDateTime(DateTime.Today);

            var leases = await db.Leases.ToListAsync();

            Assert.Equal(2, leases.Count(l => l.Covers(today)));
            Assert.Equal(1, leases.Count(l => l.EndDate < today));
            Assert.Equal(3, leases.Select(l => l.UnitId).Distinct().Count());
        }

        [Fact]
        public async Task Every_application_has_history_that_matches_its_status()
        {
            using var db = CreateSeededContext();
            await SeedAllAsync(db);

            var applications = await db.RentalApplications.Include(a => a.StatusHistory).ToListAsync();

            foreach (var application in applications)
            {
                var history = application.StatusHistory.OrderBy(h => h.ChangedAt).ToList();
                Assert.Null(history.First().FromStatus);
                Assert.Equal(ApplicationStatus.Draft, history.First().ToStatus);
                Assert.Equal(application.Status, history.Last().ToStatus);

                // Each step starts from where the previous one ended.
                for (var i = 1; i < history.Count; i++)
                    Assert.Equal(history[i - 1].ToStatus, history[i].FromStatus);
            }
        }

        [Fact]
        public async Task Only_applications_that_were_submitted_have_a_submitted_date()
        {
            using var db = CreateSeededContext();
            await SeedAllAsync(db);

            var applications = await db.RentalApplications.Include(a => a.StatusHistory).ToListAsync();

            foreach (var application in applications)
            {
                var wasSubmitted = application.StatusHistory.Any(h => h.ToStatus == ApplicationStatus.Submitted);
                Assert.Equal(wasSubmitted, application.SubmittedAt is not null);
            }
        }

        [Fact]
        public async Task Seeded_residences_never_move_out_before_they_move_in()
        {
            using var db = CreateSeededContext();
            await SeedAllAsync(db);

            var residences = await db.Residences.ToListAsync();

            Assert.NotEmpty(residences);
            Assert.All(residences, r => Assert.True(r.MoveOutDate >= r.MoveInDate));
        }

        [Fact]
        public async Task One_seeded_unit_uses_the_inactive_unit_type()
        {
            using var db = CreateSeededContext();
            await SeedAllAsync(db);

            var inactiveUnits = await db.Units.Where(u => !u.UnitType.IsActive).ToListAsync();

            Assert.Single(inactiveUnits);
        }

        [Fact]
        public async Task Properties_cannot_be_seeded_without_unit_types()
        {
            using var db = CreateContext();

            await Assert.ThrowsAsync<InvalidOperationException>(() => DemoPropertySeeder.SeedAsync(db));
        }

        [Fact]
        public async Task Applications_cannot_be_seeded_without_users()
        {
            using var db = CreateContext();
            AddUnitTypes(db);
            var units = await DemoPropertySeeder.SeedAsync(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => DemoApplicationSeeder.SeedAsync(db, units));
        }

        private static async Task<(int, int, int, int, int, int)> CountsAsync(AppDbContext db) => (
            await db.Properties.CountAsync(),
            await db.Units.CountAsync(),
            await db.RentalApplications.CountAsync(),
            await db.Residences.CountAsync(),
            await db.ApplicationStatusHistories.CountAsync(),
            await db.Leases.CountAsync());
    }
}
