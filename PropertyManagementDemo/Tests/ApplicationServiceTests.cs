using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Infrastructure.Data;
using Infrastructure.Dtos;
using Infrastructure.Mapping;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Tests
{
    public class ApplicationServiceTests
    {
        private sealed class Fixture : IDisposable
        {
            public required AppDbContext Db { get; init; }
            public required ApplicationService Service { get; init; }
            public required string ApplicantId { get; init; }
            public required int UnitId { get; init; }

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
            var unit = new Unit { Property = property, UnitType = unitType, UnitNumber = "101", Bedrooms = 1, MonthlyRent = 1000m };
            var applicant = new ApplicationUser
            {
                Id = "applicant-1",
                UserName = "jane@test.com",
                Email = "jane@test.com",
                PhoneNumber = "555-0100",
                FullName = "Jane Doe",
                CurrentAddress = "9 Elm St"
            };
            db.Units.Add(unit);
            db.Users.Add(applicant);
            db.SaveChanges();

            var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();

            return new Fixture
            {
                Db = db,
                Service = new ApplicationService(db, mapper),
                ApplicantId = applicant.Id,
                UnitId = unit.Id
            };
        }

        private static ResidenceInputDto Residence(int applicationId, DateOnly? moveIn = null, DateOnly? moveOut = null) => new()
        {
            ApplicationId = applicationId,
            Address = "5 Old Rd",
            LandlordName = "Bob",
            LandlordPhone = "555-0199",
            MoveInDate = moveIn ?? new DateOnly(2022, 1, 1),
            MoveOutDate = moveOut ?? new DateOnly(2024, 1, 1)
        };

        private static ApplicantInfoInputDto Info(int applicationId) => new()
        {
            ApplicationId = applicationId,
            FullName = "  Jane Q. Doe ",
            Phone = "555-0111",
            Email = "jane.q@test.com",
            CurrentAddress = "10 Elm St"
        };

        private static async Task<int> StartReadyToSubmitAsync(Fixture f)
        {
            var id = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            await f.Service.SaveApplicantInfoAsync(f.ApplicantId, Info(id));
            await f.Service.SaveResidenceAsync(f.ApplicantId, Residence(id));
            await f.Service.CompleteResidenceHistoryAsync(f.ApplicantId, id);
            return id;
        }

        [Fact]
        public async Task Start_creates_draft_prefilled_from_the_account_with_a_history_row()
        {
            using var f = CreateFixture();

            var id = await f.Service.StartAsync(f.ApplicantId, f.UnitId);

            var application = await f.Service.GetByIdAsync(id, f.ApplicantId);
            Assert.NotNull(application);
            Assert.Equal(ApplicationStatus.Draft, application.Status);
            Assert.Equal("Jane Doe", application.FullName);
            Assert.Equal("jane@test.com", application.Email);
            Assert.Equal("555-0100", application.Phone);
            Assert.Equal("9 Elm St", application.CurrentAddress);
            Assert.False(application.ApplicantInfoSaved);
            Assert.False(application.ResidenceHistorySaved);

            var history = await f.Db.ApplicationStatusHistories.SingleAsync();
            Assert.Null(history.FromStatus);
            Assert.Equal(ApplicationStatus.Draft, history.ToStatus);
            Assert.Equal(f.ApplicantId, history.ChangedById);
        }

        [Fact]
        public async Task Start_rejects_unknown_unit()
        {
            using var f = CreateFixture();

            await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.StartAsync(f.ApplicantId, unitId: 9999));
        }

        [Fact]
        public async Task GetById_returns_null_for_another_applicants_application()
        {
            using var f = CreateFixture();
            var id = await f.Service.StartAsync(f.ApplicantId, f.UnitId);

            var result = await f.Service.GetByIdAsync(id, "someone-else");

            Assert.Null(result);
        }

        [Fact]
        public async Task Save_applicant_info_trims_values_and_marks_section_saved()
        {
            using var f = CreateFixture();
            var id = await f.Service.StartAsync(f.ApplicantId, f.UnitId);

            await f.Service.SaveApplicantInfoAsync(f.ApplicantId, Info(id));

            var application = await f.Service.GetByIdAsync(id, f.ApplicantId);
            Assert.Equal("Jane Q. Doe", application!.FullName);
            Assert.True(application.ApplicantInfoSaved);
        }

        [Fact]
        public async Task Save_applicant_info_is_rejected_for_another_applicant()
        {
            using var f = CreateFixture();
            var id = await f.Service.StartAsync(f.ApplicantId, f.UnitId);

            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                f.Service.SaveApplicantInfoAsync("someone-else", Info(id)));
        }

        [Theory]
        [InlineData(ApplicationStatus.Submitted)]
        [InlineData(ApplicationStatus.Approved)]
        [InlineData(ApplicationStatus.Denied)]
        [InlineData(ApplicationStatus.Withdrawn)]
        public async Task Sections_cannot_be_edited_outside_draft_or_returned(ApplicationStatus status)
        {
            using var f = CreateFixture();
            var id = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            (await f.Db.RentalApplications.SingleAsync()).Status = status;
            await f.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                f.Service.SaveApplicantInfoAsync(f.ApplicantId, Info(id)));
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                f.Service.SaveResidenceAsync(f.ApplicantId, Residence(id)));
        }

        [Fact]
        public async Task Save_residence_rejects_move_out_before_move_in()
        {
            using var f = CreateFixture();
            var id = await f.Service.StartAsync(f.ApplicantId, f.UnitId);

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                f.Service.SaveResidenceAsync(f.ApplicantId,
                    Residence(id, moveIn: new DateOnly(2024, 1, 1), moveOut: new DateOnly(2023, 1, 1))));

            Assert.Equal(nameof(ResidenceInputDto.MoveOutDate), ex.Key);
            Assert.Empty(f.Db.Residences);
        }

        [Fact]
        public async Task Changing_the_residence_list_requires_confirming_it_again()
        {
            using var f = CreateFixture();
            var id = await StartReadyToSubmitAsync(f);
            Assert.True((await f.Service.GetByIdAsync(id, f.ApplicantId))!.ResidenceHistorySaved);

            await f.Service.SaveResidenceAsync(f.ApplicantId, Residence(id, new DateOnly(2018, 1, 1), new DateOnly(2021, 1, 1)));

            var application = await f.Service.GetByIdAsync(id, f.ApplicantId);
            Assert.False(application!.ResidenceHistorySaved);
            Assert.Equal(2, application.Residences.Count);
            Assert.True(application.Residences[0].MoveInDate < application.Residences[1].MoveInDate);
        }

        [Fact]
        public async Task Delete_residence_removes_it_and_requires_confirming_again()
        {
            using var f = CreateFixture();
            var id = await StartReadyToSubmitAsync(f);
            var residenceId = (await f.Service.GetByIdAsync(id, f.ApplicantId))!.Residences.Single().Id;

            await f.Service.DeleteResidenceAsync(f.ApplicantId, id, residenceId);

            var application = await f.Service.GetByIdAsync(id, f.ApplicantId);
            Assert.Empty(application!.Residences);
            Assert.False(application.ResidenceHistorySaved);
        }

        [Fact]
        public async Task Complete_residence_history_is_allowed_with_no_residences()
        {
            using var f = CreateFixture();
            var id = await f.Service.StartAsync(f.ApplicantId, f.UnitId);

            await f.Service.CompleteResidenceHistoryAsync(f.ApplicantId, id);

            var application = await f.Service.GetByIdAsync(id, f.ApplicantId);
            Assert.True(application!.ResidenceHistorySaved);
            Assert.Empty(application.Residences);
        }

        [Fact]
        public async Task Submit_is_blocked_until_both_sections_are_saved()
        {
            using var f = CreateFixture();
            var id = await f.Service.StartAsync(f.ApplicantId, f.UnitId);

            await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SubmitAsync(f.ApplicantId, id));

            await f.Service.SaveApplicantInfoAsync(f.ApplicantId, Info(id));
            await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SubmitAsync(f.ApplicantId, id));
        }

        [Fact]
        public async Task Submit_moves_to_submitted_and_records_history()
        {
            using var f = CreateFixture();
            var id = await StartReadyToSubmitAsync(f);

            await f.Service.SubmitAsync(f.ApplicantId, id);

            var application = await f.Service.GetByIdAsync(id, f.ApplicantId);
            Assert.Equal(ApplicationStatus.Submitted, application!.Status);
            Assert.NotNull(application.SubmittedAt);

            var last = await f.Db.ApplicationStatusHistories
                .OrderByDescending(h => h.Id).FirstAsync();
            Assert.Equal(ApplicationStatus.Draft, last.FromStatus);
            Assert.Equal(ApplicationStatus.Submitted, last.ToStatus);
        }

        [Fact]
        public async Task Submitted_application_cannot_be_submitted_again()
        {
            using var f = CreateFixture();
            var id = await StartReadyToSubmitAsync(f);
            await f.Service.SubmitAsync(f.ApplicantId, id);

            await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SubmitAsync(f.ApplicantId, id));
        }

        [Theory]
        [InlineData(ApplicationStatus.Draft)]
        [InlineData(ApplicationStatus.Submitted)]
        [InlineData(ApplicationStatus.Returned)]
        public async Task Withdraw_is_allowed_from_open_statuses(ApplicationStatus status)
        {
            using var f = CreateFixture();
            var id = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            (await f.Db.RentalApplications.SingleAsync()).Status = status;
            await f.Db.SaveChangesAsync();

            await f.Service.WithdrawAsync(f.ApplicantId, id);

            var application = await f.Service.GetByIdAsync(id, f.ApplicantId);
            Assert.Equal(ApplicationStatus.Withdrawn, application!.Status);
        }

        [Theory]
        [InlineData(ApplicationStatus.Approved)]
        [InlineData(ApplicationStatus.Denied)]
        [InlineData(ApplicationStatus.Withdrawn)]
        public async Task Withdraw_is_rejected_from_terminal_statuses(ApplicationStatus status)
        {
            using var f = CreateFixture();
            var id = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            (await f.Db.RentalApplications.SingleAsync()).Status = status;
            await f.Db.SaveChangesAsync();

            await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.WithdrawAsync(f.ApplicantId, id));
        }

        [Fact]
        public async Task Returned_application_can_be_corrected_and_resubmitted()
        {
            using var f = CreateFixture();
            var id = await StartReadyToSubmitAsync(f);
            await f.Service.SubmitAsync(f.ApplicantId, id);
            (await f.Db.RentalApplications.SingleAsync()).Status = ApplicationStatus.Returned;
            await f.Db.SaveChangesAsync();

            await f.Service.SaveApplicantInfoAsync(f.ApplicantId, Info(id));
            await f.Service.SubmitAsync(f.ApplicantId, id);

            var application = await f.Service.GetByIdAsync(id, f.ApplicantId);
            Assert.Equal(ApplicationStatus.Submitted, application!.Status);
        }
    }
}
