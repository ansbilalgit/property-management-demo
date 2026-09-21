using Bogus;
using Domain.Constants;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Seeding
{
    public static class DemoUserSeeder
    {
        public const string Password = "Demo123";

        public static readonly string[] ManagerEmails = ["manager1@demo.com", "manager2@demo.com"];

        public static readonly string[] ApplicantEmails =
        [
            "applicant1@demo.com",
            "applicant2@demo.com",
            "applicant3@demo.com",
            "applicant4@demo.com",
            "applicant5@demo.com"
        ];

        // Fixed emails make the accounts predictable; the fixed Bogus seed makes their names, phones and
        // addresses the same on every run. A user is only created when its email does not exist yet.
        public static async Task SeedAsync(UserManager<ApplicationUser> userManager)
        {
            var faker = new Faker("en_US") { Random = new Randomizer(1234) };

            foreach (var email in ManagerEmails)
                await EnsureUserAsync(userManager, faker, email, Roles.PropertyManager, withAddress: false);

            foreach (var email in ApplicantEmails)
                await EnsureUserAsync(userManager, faker, email, Roles.Applicant, withAddress: true);
        }

        private static async Task EnsureUserAsync(
            UserManager<ApplicationUser> userManager, Faker faker, string email, string role, bool withAddress)
        {
            // Generated before the existence check, so every user consumes the same random values on every run.
            var fullName = faker.Name.FullName();
            var phone = faker.Phone.PhoneNumber("###-###-####");
            var address = $"{faker.Address.StreetAddress()}, {faker.Address.City()}, {faker.Address.StateAbbr()} {faker.Address.ZipCode("#####")}";

            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null)
                return;

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                PhoneNumber = phone,
                CurrentAddress = withAddress ? address : null
            };

            var created = await userManager.CreateAsync(user, Password);
            if (!created.Succeeded)
                throw new InvalidOperationException($"Could not seed user {email}: {string.Join("; ", created.Errors.Select(e => e.Description))}");

            var roleAdded = await userManager.AddToRoleAsync(user, role);
            if (!roleAdded.Succeeded)
                throw new InvalidOperationException($"Could not add {email} to role {role}: {string.Join("; ", roleAdded.Errors.Select(e => e.Description))}");
        }
    }
}
