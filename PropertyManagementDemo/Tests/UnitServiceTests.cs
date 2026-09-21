using AutoMapper;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Mapping;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Tests
{
    public class UnitServiceTests
    {
        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

        private sealed class Fixture : IDisposable
        {
            public required AppDbContext Db { get; init; }
            public required UnitService Service { get; init; }
            public required int LeasedUnitId { get; init; }
            public required int ExpiredLeaseUnitId { get; init; }
            public required int NeverLeasedUnitId { get; init; }

            public void Dispose() => Db.Dispose();
        }

        private static Fixture CreateFixture()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new AppDbContext(options);

            var unitType = new UnitType { Name = "Studio", IsActive = true };
            var property = new Property { Name = "Oak", AddressLine = "1 Main", City = "Austin", State = "TX", PostalCode = "78701" };

            Unit NewUnit(string number) => new()
            {
                Property = property,
                UnitType = unitType,
                UnitNumber = number,
                Bedrooms = 1,
                MonthlyRent = 1000m
            };

            var leased = NewUnit("101");
            var expired = NewUnit("102");
            var free = NewUnit("103");
            db.Units.AddRange(leased, expired, free);
            db.SaveChanges();

            db.Leases.Add(Lease.ForTwelveMonths(leased.Id, "tenant-1", rentalApplicationId: 1, Today.AddMonths(-3)));
            db.Leases.Add(Lease.ForTwelveMonths(expired.Id, "tenant-2", rentalApplicationId: 2, Today.AddMonths(-24)));
            db.SaveChanges();

            var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

            return new Fixture
            {
                Db = db,
                Service = new UnitService(db, mapper),
                LeasedUnitId = leased.Id,
                ExpiredLeaseUnitId = expired.Id,
                NeverLeasedUnitId = free.Id
            };
        }

        [Fact]
        public async Task Available_units_exclude_units_with_a_lease_covering_today()
        {
            using var f = CreateFixture();

            var available = await f.Service.GetAvailableUnitsAsync();

            Assert.Equal(["102", "103"], available.Select(u => u.UnitNumber));
        }

        [Fact]
        public async Task Units_by_property_show_availability_for_each_unit()
        {
            using var f = CreateFixture();
            var propertyId = (await f.Service.GetByIdAsync(f.LeasedUnitId))!.PropertyId;

            var units = await f.Service.GetUnitsByPropertyAsync(propertyId);

            Assert.Equal(3, units.Count);
            Assert.False(units.Single(u => u.Id == f.LeasedUnitId).IsAvailable);
            Assert.True(units.Single(u => u.Id == f.ExpiredLeaseUnitId).IsAvailable);
            Assert.True(units.Single(u => u.Id == f.NeverLeasedUnitId).IsAvailable);
        }

        [Fact]
        public async Task Get_by_id_reports_availability()
        {
            using var f = CreateFixture();

            Assert.False((await f.Service.GetByIdAsync(f.LeasedUnitId))!.IsAvailable);
            Assert.True((await f.Service.GetByIdAsync(f.NeverLeasedUnitId))!.IsAvailable);
        }
    }
}
