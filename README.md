# Property Management Demo

A small web app for a property management company. Applicants apply for apartments. Property managers review the applications. An approved application becomes a 12-month lease.

Built with ASP.NET Core MVC (Razor), .NET 10, Entity Framework Core and SQL Server.

## What you need

- .NET 10 SDK
- SQL Server or SQL Server Express

## How to run

1. Open a terminal in the `PropertyManagementDemo` folder (the one that contains `PropertyManagementDemo.slnx`).
2. Start the app:

   ```
   dotnet run --project PropertyManagementDemo
   ```

3. Open `http://localhost:5275` in your browser.

On start, the app creates the database, applies the migrations and adds demo data. You do not need to do anything else. Running it again does not add duplicates.

### Different SQL Server?

The app expects SQL Server Express on this machine (`.\SQLEXPRESS`). To use another server, set the connection string before you run:

```
set ConnectionStrings__DefaultConnection=Server=(localdb)\MSSQLLocalDB;Database=PropertyManagement;Trusted_Connection=True;TrustServerCertificate=True
```

Or change `DefaultConnection` in `PropertyManagementDemo/PropertyManagementDemo/appsettings.json`.

## Demo logins

The password for every account is `Demo123`.

| Role | Email |
| --- | --- |
| Property manager | `manager1@demo.com` |
| Property manager | `manager2@demo.com` |
| Applicant | `applicant1@demo.com` ... `applicant5@demo.com` |

You can also sign up and choose Applicant or Property Manager.

## What each role can do

**Applicant**
- Browse available units and start an application.
- Fill in the application one section at a time (Applicant information, Residence history, then Summary). Add, edit and remove residences in a pop-up.
- Submit, withdraw, and correct and resubmit an application that was returned.
- See their own applications, and filter them by status and property.

**Property manager**
- Add, edit and remove properties and units in pop-ups.
- Review submitted applications: Approve, Return or Deny. A comment is required for Return and Deny.
- See the history of each application (who, when, comment).
- See all submitted applications, and filter them by status and property.

## Demo data

The seed data has 2 managers, 5 applicants, 4 properties with 20 units, and 13 applications in every status (Draft, Submitted, Returned, Approved, Denied, Withdrawn). Names and addresses are made by Bogus.

## Run the tests

From the same folder:

```
dotnet test
```

The tests cover the business rules (applications, reviews, leases, units, properties) and the seeding. They use an in-memory database, so SQL Server is not needed.

## Project structure

| Folder | What is inside |
| --- | --- |
| `Domain` | Entities, enums and business rules. The application rules live in `RentalApplication`. |
| `Infrastructure` | Database (EF Core, migrations), Identity setup and seeding. |
| `Services` | Application services, DTOs and mapping (AutoMapper). |
| `PropertyManagementDemo` | The web app: controllers, view models, views, view components. |
| `Tests` | Unit tests. |

## Good to know

- An application is only editable while it is Draft or Returned. Approved, Denied and Withdrawn are final.
- A lease starts on the day of approval and runs for 12 months. A unit with a lease covering today is not available.
- Submitting and approving are both blocked if the unit already has an active lease.
- A unit type that is marked inactive still shows on units that already use it, but cannot be chosen for other units. The server enforces this.
- Managers do not see drafts. A draft is not theirs to see until the applicant submits it.
- There is no screen to edit unit types. They are added by the seed data (Studio, Apartment, Townhouse, and Loft, which is inactive).
- AutoMapper 16 needs a license for commercial use. It is fine for this demo.
