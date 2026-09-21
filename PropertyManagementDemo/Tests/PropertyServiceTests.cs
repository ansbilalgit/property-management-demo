using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Services;
using Services.Dtos;
using Services.Mapping;

namespace Tests
{
    public class PropertyServiceTests
    {
        private sealed class Fixture : IDisposable
        {
            public required AppDbContext Db { get; init; }
            public required PropertyService Service { get; init; }
            public required int UnitTypeId { get; init; }

            public void Dispose() => Db.Dispose();
        }

        private static Fixture CreateFixture()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new AppDbContext(options);

            var unitType = new UnitType { Name = "Studio", IsActive = true };
            db.UnitTypes.Add(unitType);
            db.SaveChanges();

            var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

            return new Fixture { Db = db, Service = new PropertyService(db, mapper), UnitTypeId = unitType.Id };
        }

        private static PropertyInputDto Input(int? id = null, string name = "Oak Court") => new()
        {
            Id = id,
            Name = name,
            AddressLine = "1 Main St",
            City = "Austin",
            State = "TX",
            PostalCode = "78701"
        };

        private static async Task<Property> AddPropertyWithUnitsAsync(Fixture f, string name, int unitCount)
        {
            var property = new Property { Name = name, AddressLine = "1 Main St", City = "Austin", State = "TX", PostalCode = "78701" };
            for (var i = 1; i <= unitCount; i++)
            {
                property.Units.Add(new Unit
                {
                    UnitNumber = $"10{i}",
                    Bedrooms = 1,
                    MonthlyRent = 1000m,
                    UnitTypeId = f.UnitTypeId
                });
            }

            f.Db.Properties.Add(property);
            await f.Db.SaveChangesAsync();
            return property;
        }

        [Fact]
        public async Task Save_creates_a_property_with_trimmed_values()
        {
            using var f = CreateFixture();
            var input = Input(name: "  Oak Court  ");
            input.City = " Austin ";

            await f.Service.SaveAsync(input);

            var saved = await f.Db.Properties.SingleAsync();
            Assert.Equal("Oak Court", saved.Name);
            Assert.Equal("Austin", saved.City);
        }

        [Fact]
        public async Task Save_updates_an_existing_property()
        {
            using var f = CreateFixture();
            var property = await AddPropertyWithUnitsAsync(f, "Oak Court", 0);

            var input = Input(property.Id, "Oak Court Residences");
            input.PostalCode = "78702";
            await f.Service.SaveAsync(input);

            var saved = await f.Db.Properties.SingleAsync();
            Assert.Equal("Oak Court Residences", saved.Name);
            Assert.Equal("78702", saved.PostalCode);
        }

        [Fact]
        public async Task Save_rejects_a_property_that_no_longer_exists()
        {
            using var f = CreateFixture();

            await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync(Input(id: 9999)));

            Assert.Empty(f.Db.Properties);
        }

        [Fact]
        public async Task Get_by_id_returns_the_property_with_its_unit_count()
        {
            using var f = CreateFixture();
            var property = await AddPropertyWithUnitsAsync(f, "Oak Court", 3);

            var dto = await f.Service.GetByIdAsync(property.Id);

            Assert.NotNull(dto);
            Assert.Equal("Oak Court", dto.Name);
            Assert.Equal(3, dto.UnitCount);
        }

        [Fact]
        public async Task Get_by_id_returns_null_for_an_unknown_property()
        {
            using var f = CreateFixture();

            Assert.Null(await f.Service.GetByIdAsync(9999));
        }

        [Fact]
        public async Task List_is_ordered_by_name_and_counts_units()
        {
            using var f = CreateFixture();
            await AddPropertyWithUnitsAsync(f, "Maple Court", 2);
            await AddPropertyWithUnitsAsync(f, "Cedar Heights", 1);

            var list = await f.Service.ListAsync();

            Assert.Equal(["Cedar Heights", "Maple Court"], list.Select(p => p.Name));
            Assert.Equal([1, 2], list.Select(p => p.UnitCount));
        }

        [Fact]
        public async Task Delete_removes_the_property_and_its_units()
        {
            using var f = CreateFixture();
            var property = await AddPropertyWithUnitsAsync(f, "Oak Court", 2);
            await AddPropertyWithUnitsAsync(f, "Maple Court", 1);

            await f.Service.DeleteAsync(property.Id);

            Assert.Equal(["Maple Court"], await f.Db.Properties.Select(p => p.Name).ToListAsync());
            Assert.Single(f.Db.Units);
        }

        [Fact]
        public async Task Deleting_an_unknown_property_does_nothing()
        {
            using var f = CreateFixture();
            await AddPropertyWithUnitsAsync(f, "Oak Court", 1);

            await f.Service.DeleteAsync(9999);

            Assert.Single(f.Db.Properties);
        }

        [Fact]
        public async Task Delete_is_rejected_when_a_unit_has_an_application()
        {
            using var f = CreateFixture();
            var property = await AddPropertyWithUnitsAsync(f, "Oak Court", 2);
            f.Db.RentalApplications.Add(new RentalApplication
            {
                UnitId = property.Units.First().Id,
                ApplicantId = "applicant-1",
                Status = ApplicationStatus.Draft,
                CreatedAt = DateTime.UtcNow
            });
            await f.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync(property.Id));

            Assert.Single(f.Db.Properties);
            Assert.Equal(2, await f.Db.Units.CountAsync());
        }

        [Fact]
        public async Task Delete_is_rejected_when_a_unit_has_lease_history()
        {
            using var f = CreateFixture();
            var property = await AddPropertyWithUnitsAsync(f, "Oak Court", 1);
            f.Db.Leases.Add(Lease.ForTwelveMonths(
                property.Units.Single().Id, "tenant-1", rentalApplicationId: 1, new DateOnly(2020, 1, 1)));
            await f.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync(property.Id));

            Assert.Single(f.Db.Properties);
        }
    }
}
