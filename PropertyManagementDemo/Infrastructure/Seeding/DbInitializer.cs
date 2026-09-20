using Domain.Constants;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Seeding
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<AppDbContext>();
            await context.Database.MigrateAsync();

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { Roles.Applicant, Roles.PropertyManager })
            {
                var roleExists = await roleManager.RoleExistsAsync(role);
                if (!roleExists)
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            await SeedUnitTypesAsync(context);
        }

        private static async Task SeedUnitTypesAsync(AppDbContext context)
        {
            var unitTypes = new[]
            {
                new UnitType { Name = "Studio", IsActive = true },
                new UnitType { Name = "Apartment", IsActive = true },
                new UnitType { Name = "Townhouse", IsActive = true },
                new UnitType { Name = "Loft", IsActive = false }
            };

            var existing = await context.UnitTypes.Select(t => t.Name).ToListAsync();
            var missing = unitTypes.Where(t => !existing.Contains(t.Name)).ToList();
            if (missing.Count == 0)
                return;

            context.UnitTypes.AddRange(missing);
            await context.SaveChangesAsync();
        }
    }
}
