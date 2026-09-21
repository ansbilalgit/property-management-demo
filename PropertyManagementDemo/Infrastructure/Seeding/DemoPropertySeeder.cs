using Bogus;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Seeding
{
    public static class DemoPropertySeeder
    {
        private const string InactiveTypeName = "Loft";
        private const string PropertyWithInactiveTypeUnit = "Cedar Heights";

        private static readonly (string Name, int UnitCount)[] Properties =
        [
            ("Maple Court", 5),
            ("Riverside Lofts", 4),
            ("Harbor View Apartments", 6),
            ("Cedar Heights", 5)
        ];

        // Returns every seeded unit, existing or new, in a stable order (by property, then position).
        // Properties are matched by name and units by number within the property, so reruns add nothing.
        public static async Task<List<Unit>> SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
        {
            var faker = new Faker("en_US") { Random = new Randomizer(5678) };

            var unitTypes = await context.UnitTypes.ToListAsync(cancellationToken);
            var activeTypes = unitTypes.Where(t => t.IsActive).OrderBy(t => t.Name).ToList();
            if (activeTypes.Count == 0)
                throw new InvalidOperationException("Unit types must be seeded before properties.");

            // One unit deliberately uses an inactive type, to show the "still displays where already used" rule.
            var inactiveType = unitTypes.FirstOrDefault(t => t.Name == InactiveTypeName && !t.IsActive);

            var units = new List<Unit>();
            var typeIndex = 0;

            foreach (var (name, unitCount) in Properties)
            {
                // Generated before any existence check, so the random sequence is the same on every run.
                var addressLine = faker.Address.StreetAddress();
                var city = faker.Address.City();
                var state = faker.Address.StateAbbr();
                var postalCode = faker.Address.ZipCode("#####");

                var property = await context.Properties
                    .Include(p => p.Units)
                    .FirstOrDefaultAsync(p => p.Name == name, cancellationToken);

                if (property is null)
                {
                    property = new Property
                    {
                        Name = name,
                        AddressLine = addressLine,
                        City = city,
                        State = state,
                        PostalCode = postalCode
                    };
                    context.Properties.Add(property);
                }

                for (var i = 0; i < unitCount; i++)
                {
                    var floor = i / 3 + 1;
                    var unitNumber = (floor * 100 + i % 3 + 1).ToString();

                    var usesInactiveType = inactiveType is not null && name == PropertyWithInactiveTypeUnit && i == unitCount - 1;
                    var unitType = usesInactiveType ? inactiveType! : activeTypes[typeIndex++ % activeTypes.Count];

                    var bedrooms = BedroomsFor(unitType.Name, faker);
                    var monthlyRent = 700m + bedrooms * 450m + faker.Random.Int(0, 20) * 10m;

                    var unit = property.Units.FirstOrDefault(u => u.UnitNumber == unitNumber);
                    if (unit is null)
                    {
                        unit = new Unit
                        {
                            UnitNumber = unitNumber,
                            UnitType = unitType,
                            Bedrooms = bedrooms,
                            MonthlyRent = monthlyRent
                        };
                        property.Units.Add(unit);
                    }

                    units.Add(unit);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            return units;
        }

        private static int BedroomsFor(string unitTypeName, Faker faker) => unitTypeName switch
        {
            "Studio" => 0,
            "Townhouse" => faker.Random.Int(2, 4),
            "Loft" => 1,
            _ => faker.Random.Int(1, 3)
        };
    }
}
