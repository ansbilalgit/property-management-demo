using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Infrastructure.Data;
using Services.Dtos;
using Services.Mapping;
using Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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

            var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

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

        private const string SecondApplicantId = "applicant-2";

        private static async Task AddSecondApplicantAsync(Fixture f)
        {
            f.Db.Users.Add(new ApplicationUser
            {
                Id = SecondApplicantId,
                UserName = "sam@test.com",
                Email = "sam@test.com",
                FullName = "Sam Roe"
            });
            await f.Db.SaveChangesAsync();
        }

        private static async Task<(int UnitId, int PropertyId)> AddUnitInNewPropertyAsync(Fixture f, string propertyName)
        {
            var unitTypeId = await f.Db.UnitTypes.Select(t => t.Id).FirstAsync();
            var unit = new Unit
            {
                Property = new Property { Name = propertyName, AddressLine = "2 Side", City = "Austin", State = "TX", PostalCode = "78702" },
                UnitTypeId = unitTypeId,
                UnitNumber = "201",
                Bedrooms = 2,
                MonthlyRent = 1500m
            };
            f.Db.Units.Add(unit);
            await f.Db.SaveChangesAsync();
            return (unit.Id, unit.PropertyId);
        }

        [Fact]
        public async Task Applicant_list_contains_only_their_own_applications()
        {
            using var f = CreateFixture();
            await AddSecondApplicantAsync(f);
            var mine = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            await f.Service.StartAsync(SecondApplicantId, f.UnitId);

            var result = await f.Service.GetApplicantApplicationsAsync(f.ApplicantId, new ApplicationListFilterDto());

            Assert.Equal([mine], result.Select(a => a.Id));
        }

        [Fact]
        public async Task Manager_list_contains_every_applicants_submitted_applications()
        {
            using var f = CreateFixture();
            await AddSecondApplicantAsync(f);
            await StartAndSubmitAsync(f, f.ApplicantId);
            await StartAndSubmitAsync(f, SecondApplicantId);

            var result = await f.Service.GetAllApplicationsAsync(new ApplicationListFilterDto());

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task Managers_do_not_see_applications_that_were_never_submitted()
        {
            using var f = CreateFixture();
            var draft = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            var withdrawnDraft = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            await f.Service.WithdrawAsync(f.ApplicantId, withdrawnDraft);

            Assert.Empty(await f.Service.GetAllApplicationsAsync(new ApplicationListFilterDto()));
            Assert.Null(await f.Service.GetForReviewAsync(draft));
            Assert.Null(await f.Service.GetForReviewAsync(withdrawnDraft));

            var submitted = await StartAndSubmitAsync(f, f.ApplicantId);

            Assert.Equal([submitted], (await f.Service.GetAllApplicationsAsync(new ApplicationListFilterDto())).Select(a => a.Id));
            var review = await f.Service.GetForReviewAsync(submitted);
            Assert.NotNull(review);
            Assert.Single(review.Residences);
            Assert.Equal(2, review.History.Count);
            Assert.Equal("Jane Doe", review.History[0].ChangedByName);
        }

        [Fact]
        public async Task Applicants_still_see_their_own_drafts()
        {
            using var f = CreateFixture();
            var draft = await f.Service.StartAsync(f.ApplicantId, f.UnitId);

            var result = await f.Service.GetApplicantApplicationsAsync(f.ApplicantId, new ApplicationListFilterDto());

            Assert.Equal([draft], result.Select(a => a.Id));
        }

        [Fact]
        public async Task List_can_be_filtered_by_status()
        {
            using var f = CreateFixture();
            var draft = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            var withdrawn = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            await f.Service.WithdrawAsync(f.ApplicantId, withdrawn);

            var result = await f.Service.GetApplicantApplicationsAsync(
                f.ApplicantId, new ApplicationListFilterDto { Status = ApplicationStatus.Withdrawn });

            Assert.Equal([withdrawn], result.Select(a => a.Id));
            Assert.DoesNotContain(result, a => a.Id == draft);
        }

        [Fact]
        public async Task List_can_be_filtered_by_property()
        {
            using var f = CreateFixture();
            var (otherUnitId, otherPropertyId) = await AddUnitInNewPropertyAsync(f, "Elm Court");
            await StartAndSubmitAsync(f, f.ApplicantId);
            var atElm = await StartAndSubmitAsync(f, f.ApplicantId, otherUnitId);

            var result = await f.Service.GetAllApplicationsAsync(new ApplicationListFilterDto { PropertyId = otherPropertyId });

            var row = Assert.Single(result);
            Assert.Equal(atElm, row.Id);
            Assert.Equal("Elm Court", row.PropertyName);
        }

        [Fact]
        public async Task Status_and_property_filters_combine()
        {
            using var f = CreateFixture();
            var (otherUnitId, otherPropertyId) = await AddUnitInNewPropertyAsync(f, "Elm Court");
            var submittedAtElm = await StartAndSubmitAsync(f, f.ApplicantId, otherUnitId);
            var withdrawnAtElm = await StartAndSubmitAsync(f, f.ApplicantId, otherUnitId);
            await f.Service.WithdrawAsync(f.ApplicantId, withdrawnAtElm);
            var withdrawnAtOak = await StartAndSubmitAsync(f, f.ApplicantId);
            await f.Service.WithdrawAsync(f.ApplicantId, withdrawnAtOak);

            var result = await f.Service.GetAllApplicationsAsync(new ApplicationListFilterDto
            {
                Status = ApplicationStatus.Withdrawn,
                PropertyId = otherPropertyId
            });

            Assert.Equal([withdrawnAtElm], result.Select(a => a.Id));
            Assert.DoesNotContain(result, a => a.Id == submittedAtElm);
        }

        [Fact]
        public async Task List_shows_newest_application_first()
        {
            using var f = CreateFixture();
            var older = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            var newer = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            (await f.Db.RentalApplications.SingleAsync(a => a.Id == older)).CreatedAt = DateTime.UtcNow.AddDays(-1);
            await f.Db.SaveChangesAsync();

            var result = await f.Service.GetApplicantApplicationsAsync(f.ApplicantId, new ApplicationListFilterDto());

            Assert.Equal([newer, older], result.Select(a => a.Id));
        }

        [Fact]
        public async Task List_rows_skip_residences_but_get_by_id_loads_them()
        {
            using var f = CreateFixture();
            var id = await StartReadyToSubmitAsync(f);

            var list = await f.Service.GetApplicantApplicationsAsync(f.ApplicantId, new ApplicationListFilterDto());
            var detail = await f.Service.GetByIdAsync(id, f.ApplicantId);

            Assert.Empty(list.Single().Residences);
            Assert.Single(detail!.Residences);
        }

        private const string ManagerId = "manager-1";

        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

        private static ReviewInputDto Review(int applicationId, ReviewOutcome outcome, string? comment = null) => new()
        {
            ApplicationId = applicationId,
            Outcome = outcome,
            Comment = comment
        };

        private static async Task<int> StartAndSubmitAsync(Fixture f, string applicantId, int? unitId = null)
        {
            var id = await f.Service.StartAsync(applicantId, unitId ?? f.UnitId);
            await f.Service.SaveApplicantInfoAsync(applicantId, Info(id));
            await f.Service.SaveResidenceAsync(applicantId, Residence(id));
            await f.Service.CompleteResidenceHistoryAsync(applicantId, id);
            await f.Service.SubmitAsync(applicantId, id);
            return id;
        }

        private static async Task AddLeaseAsync(Fixture f, DateOnly start)
        {
            f.Db.Leases.Add(Lease.ForTwelveMonths(f.UnitId, "previous-tenant", rentalApplicationId: 9000, start));
            await f.Db.SaveChangesAsync();
        }

        [Fact]
        public async Task Approve_creates_a_twelve_month_lease_and_records_the_review()
        {
            using var f = CreateFixture();
            var id = await StartAndSubmitAsync(f, f.ApplicantId);

            await f.Service.ReviewAsync(ManagerId, Review(id, ReviewOutcome.Approve, "  Welcome  "));

            var application = await f.Service.GetByIdAsync(id, f.ApplicantId);
            Assert.Equal(ApplicationStatus.Approved, application!.Status);

            var lease = await f.Db.Leases.SingleAsync();
            Assert.Equal(f.UnitId, lease.UnitId);
            Assert.Equal(f.ApplicantId, lease.ApplicantId);
            Assert.Equal(id, lease.RentalApplicationId);
            Assert.Equal(Today, lease.StartDate);
            Assert.Equal(Today.AddMonths(12).AddDays(-1), lease.EndDate);

            var last = await f.Db.ApplicationStatusHistories.OrderByDescending(h => h.Id).FirstAsync();
            Assert.Equal(ApplicationStatus.Submitted, last.FromStatus);
            Assert.Equal(ApplicationStatus.Approved, last.ToStatus);
            Assert.Equal(ManagerId, last.ChangedById);
            Assert.Equal("Welcome", last.Comment);
        }

        [Theory]
        [InlineData(ReviewOutcome.Return)]
        [InlineData(ReviewOutcome.Deny)]
        public async Task Return_and_deny_require_a_comment(ReviewOutcome outcome)
        {
            using var f = CreateFixture();
            var id = await StartAndSubmitAsync(f, f.ApplicantId);

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                f.Service.ReviewAsync(ManagerId, Review(id, outcome, "   ")));

            Assert.Equal(nameof(ReviewInputDto.Comment), ex.Key);
            Assert.Equal(ApplicationStatus.Submitted, (await f.Service.GetByIdAsync(id, f.ApplicantId))!.Status);
        }

        [Fact]
        public async Task Deny_sets_denied_without_creating_a_lease()
        {
            using var f = CreateFixture();
            var id = await StartAndSubmitAsync(f, f.ApplicantId);

            await f.Service.ReviewAsync(ManagerId, Review(id, ReviewOutcome.Deny, "Income too low"));

            Assert.Equal(ApplicationStatus.Denied, (await f.Service.GetByIdAsync(id, f.ApplicantId))!.Status);
            Assert.Empty(f.Db.Leases);
        }

        [Fact]
        public async Task Returned_application_can_be_resubmitted_by_the_applicant()
        {
            using var f = CreateFixture();
            var id = await StartAndSubmitAsync(f, f.ApplicantId);

            await f.Service.ReviewAsync(ManagerId, Review(id, ReviewOutcome.Return, "Add a landlord phone"));
            Assert.Equal(ApplicationStatus.Returned, (await f.Service.GetByIdAsync(id, f.ApplicantId))!.Status);

            await f.Service.SubmitAsync(f.ApplicantId, id);

            Assert.Equal(ApplicationStatus.Submitted, (await f.Service.GetByIdAsync(id, f.ApplicantId))!.Status);
        }

        [Fact]
        public async Task Only_submitted_applications_can_be_reviewed()
        {
            using var f = CreateFixture();
            var draft = await f.Service.StartAsync(f.ApplicantId, f.UnitId);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                f.Service.ReviewAsync(ManagerId, Review(draft, ReviewOutcome.Approve)));

            var submitted = await StartAndSubmitAsync(f, f.ApplicantId);
            await f.Service.ReviewAsync(ManagerId, Review(submitted, ReviewOutcome.Approve));

            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                f.Service.ReviewAsync(ManagerId, Review(submitted, ReviewOutcome.Approve)));
            Assert.Single(f.Db.Leases);
        }

        [Fact]
        public async Task Submit_is_rejected_while_the_unit_has_an_active_lease()
        {
            using var f = CreateFixture();
            var id = await StartReadyToSubmitAsync(f);
            await AddLeaseAsync(f, Today.AddMonths(-3));

            await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SubmitAsync(f.ApplicantId, id));

            Assert.Equal(ApplicationStatus.Draft, (await f.Service.GetByIdAsync(id, f.ApplicantId))!.Status);
        }

        [Fact]
        public async Task An_expired_lease_does_not_block_submit()
        {
            using var f = CreateFixture();
            var id = await StartReadyToSubmitAsync(f);
            await AddLeaseAsync(f, Today.AddMonths(-24));

            await f.Service.SubmitAsync(f.ApplicantId, id);

            Assert.Equal(ApplicationStatus.Submitted, (await f.Service.GetByIdAsync(id, f.ApplicantId))!.Status);
        }

        [Fact]
        public async Task Approve_is_rejected_when_the_unit_was_leased_after_submission()
        {
            using var f = CreateFixture();
            var id = await StartAndSubmitAsync(f, f.ApplicantId);
            await AddLeaseAsync(f, Today.AddDays(-10));

            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                f.Service.ReviewAsync(ManagerId, Review(id, ReviewOutcome.Approve)));

            Assert.Equal(ApplicationStatus.Submitted, (await f.Service.GetByIdAsync(id, f.ApplicantId))!.Status);
            Assert.Single(f.Db.Leases);
        }

        [Fact]
        public async Task Approving_one_application_leaves_other_open_applications_alone()
        {
            using var f = CreateFixture();
            await AddSecondApplicantAsync(f);
            var first = await StartAndSubmitAsync(f, f.ApplicantId);
            var second = await StartAndSubmitAsync(f, SecondApplicantId);

            await f.Service.ReviewAsync(ManagerId, Review(first, ReviewOutcome.Approve));

            Assert.Equal(ApplicationStatus.Submitted, (await f.Service.GetByIdAsync(second, SecondApplicantId))!.Status);
        }
    }
}
