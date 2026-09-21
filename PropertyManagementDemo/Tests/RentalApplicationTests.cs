using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;

namespace Tests
{
    // The application's rules, tested without a database.
    public class RentalApplicationTests
    {
        private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateOnly Today = new(2026, 3, 1);

        private static RentalApplication NewDraft() =>
            RentalApplication.Start(1, "applicant-1", "Jane Doe", "jane@test.com", "555-0100", "9 Elm St", Now);

        private static void AddResidence(RentalApplication application) =>
            application.AddResidence("5 Old Rd", "Bob", "555-0199", new DateOnly(2022, 1, 1), new DateOnly(2024, 1, 1));

        private static RentalApplication NewSubmitted()
        {
            var application = NewDraft();
            application.SaveApplicantInfo("Jane Doe", "555-0100", "jane@test.com", "9 Elm St");
            application.CompleteResidenceHistory();
            application.Submit("applicant-1", Now.AddDays(1));
            return application;
        }

        [Fact]
        public void A_new_application_is_an_editable_draft_with_its_first_history_row()
        {
            var application = NewDraft();

            Assert.Equal(ApplicationStatus.Draft, application.Status);
            Assert.True(application.IsEditable);
            Assert.False(application.ApplicantInfoSaved);
            Assert.False(application.ResidenceHistorySaved);
            var history = Assert.Single(application.StatusHistory);
            Assert.Null(history.FromStatus);
            Assert.Equal(ApplicationStatus.Draft, history.ToStatus);
            Assert.Equal("applicant-1", history.ChangedById);
        }

        [Fact]
        public void Saving_applicant_info_trims_values_and_marks_the_section_saved()
        {
            var application = NewDraft();

            application.SaveApplicantInfo("  Jane Q. Doe ", " 555-0111 ", " jane.q@test.com ", " 10 Elm St ");

            Assert.Equal("Jane Q. Doe", application.FullName);
            Assert.Equal("555-0111", application.Phone);
            Assert.True(application.ApplicantInfoSaved);
        }

        [Fact]
        public void Adding_or_removing_a_residence_requires_confirming_the_list_again()
        {
            var application = NewDraft();
            application.CompleteResidenceHistory();
            Assert.True(application.ResidenceHistorySaved);

            AddResidence(application);
            Assert.False(application.ResidenceHistorySaved);

            application.CompleteResidenceHistory();
            var removed = application.RemoveResidence(application.Residences.Single().Id);
            Assert.NotNull(removed);
            Assert.Empty(application.Residences);
            Assert.False(application.ResidenceHistorySaved);
        }

        [Fact]
        public void A_residence_cannot_move_out_before_it_moved_in()
        {
            var application = NewDraft();

            var ex = Assert.Throws<BusinessRuleException>(() =>
                application.AddResidence("5 Old Rd", "Bob", "555-0199", new DateOnly(2024, 1, 1), new DateOnly(2023, 1, 1)));

            Assert.Equal("MoveOutDate", ex.Key);
            Assert.Empty(application.Residences);
        }

        [Fact]
        public void Removing_a_residence_that_is_already_gone_returns_null()
        {
            Assert.Null(NewDraft().RemoveResidence(999));
        }

        [Fact]
        public void Updating_an_unknown_residence_is_rejected()
        {
            Assert.Throws<BusinessRuleException>(() =>
                NewDraft().UpdateResidence(999, "a", "b", "c", new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1)));
        }

        [Fact]
        public void Submit_needs_both_sections_saved()
        {
            var application = NewDraft();
            Assert.Throws<BusinessRuleException>(() => application.Submit("applicant-1", Now));

            application.SaveApplicantInfo("Jane Doe", "555-0100", "jane@test.com", "9 Elm St");
            Assert.Throws<BusinessRuleException>(() => application.Submit("applicant-1", Now));

            application.CompleteResidenceHistory();
            application.Submit("applicant-1", Now.AddDays(1));

            Assert.Equal(ApplicationStatus.Submitted, application.Status);
            Assert.Equal(Now.AddDays(1), application.SubmittedAt);
        }

        [Fact]
        public void Once_submitted_the_sections_are_read_only()
        {
            var application = NewSubmitted();

            Assert.False(application.IsEditable);
            Assert.Throws<BusinessRuleException>(() => application.SaveApplicantInfo("a", "b", "c", "d"));
            Assert.Throws<BusinessRuleException>(() => AddResidence(application));
            Assert.Throws<BusinessRuleException>(() => application.CompleteResidenceHistory());
            Assert.Throws<BusinessRuleException>(() => application.Submit("applicant-1", Now));
        }

        [Fact]
        public void Approve_moves_to_approved_and_issues_a_twelve_month_lease()
        {
            var application = NewSubmitted();

            var lease = application.Approve("manager-1", "  Welcome  ", Today, Now.AddDays(3));

            Assert.Equal(ApplicationStatus.Approved, application.Status);
            Assert.Same(application, lease.RentalApplication);
            Assert.Equal(application.UnitId, lease.UnitId);
            Assert.Equal("applicant-1", lease.ApplicantId);
            Assert.Equal(Today, lease.StartDate);
            Assert.Equal(new DateOnly(2027, 2, 28), lease.EndDate);

            var last = application.StatusHistory.Last();
            Assert.Equal(ApplicationStatus.Submitted, last.FromStatus);
            Assert.Equal(ApplicationStatus.Approved, last.ToStatus);
            Assert.Equal("manager-1", last.ChangedById);
            Assert.Equal("Welcome", last.Comment);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Return_and_deny_need_a_comment(string? comment)
        {
            var toReturn = NewSubmitted();
            var toDeny = NewSubmitted();

            var returnError = Assert.Throws<BusinessRuleException>(() => toReturn.Return("manager-1", comment, Now));
            var denyError = Assert.Throws<BusinessRuleException>(() => toDeny.Deny("manager-1", comment, Now));

            Assert.Equal("Comment", returnError.Key);
            Assert.Equal("Comment", denyError.Key);
            Assert.Equal(ApplicationStatus.Submitted, toReturn.Status);
            Assert.Equal(ApplicationStatus.Submitted, toDeny.Status);
        }

        [Fact]
        public void A_returned_application_is_editable_again_and_can_be_resubmitted()
        {
            var application = NewSubmitted();

            application.Return("manager-1", "Add a landlord phone", Now.AddDays(2));
            Assert.Equal(ApplicationStatus.Returned, application.Status);
            Assert.True(application.IsEditable);

            application.SaveApplicantInfo("Jane Doe", "555-0100", "jane@test.com", "9 Elm St");
            application.Submit("applicant-1", Now.AddDays(3));

            Assert.Equal(ApplicationStatus.Submitted, application.Status);
        }

        [Fact]
        public void Only_submitted_applications_can_be_reviewed()
        {
            var draft = NewDraft();
            Assert.Throws<BusinessRuleException>(() => draft.Approve("manager-1", null, Today, Now));
            Assert.Throws<BusinessRuleException>(() => draft.Return("manager-1", "x", Now));
            Assert.Throws<BusinessRuleException>(() => draft.Deny("manager-1", "x", Now));

            var approved = NewSubmitted();
            approved.Approve("manager-1", null, Today, Now);
            Assert.Throws<BusinessRuleException>(() => approved.Approve("manager-1", null, Today, Now));
        }

        [Theory]
        [InlineData("draft")]
        [InlineData("submitted")]
        [InlineData("returned")]
        public void Withdraw_is_allowed_from_open_statuses(string state)
        {
            var application = state switch
            {
                "draft" => NewDraft(),
                "submitted" => NewSubmitted(),
                _ => ReturnedApplication()
            };

            application.Withdraw("applicant-1", Now.AddDays(5));

            Assert.Equal(ApplicationStatus.Withdrawn, application.Status);
            Assert.False(application.IsEditable);
        }

        [Theory]
        [InlineData("approved")]
        [InlineData("denied")]
        [InlineData("withdrawn")]
        public void Terminal_statuses_cannot_be_withdrawn_or_changed(string state)
        {
            var application = NewSubmitted();
            switch (state)
            {
                case "approved": application.Approve("manager-1", null, Today, Now); break;
                case "denied": application.Deny("manager-1", "No", Now); break;
                default: application.Withdraw("applicant-1", Now); break;
            }

            Assert.Throws<BusinessRuleException>(() => application.Withdraw("applicant-1", Now));
            Assert.Throws<BusinessRuleException>(() => application.Submit("applicant-1", Now));
            Assert.Throws<BusinessRuleException>(() => application.SaveApplicantInfo("a", "b", "c", "d"));
        }

        [Fact]
        public void Every_transition_adds_a_history_row_that_continues_from_the_previous_one()
        {
            var application = NewSubmitted();
            application.Return("manager-1", "Fix it", Now.AddDays(2));
            application.Submit("applicant-1", Now.AddDays(3));
            application.Deny("manager-1", "Still no", Now.AddDays(4));

            var history = application.StatusHistory.ToList();

            Assert.Equal(
                [ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.Returned, ApplicationStatus.Submitted, ApplicationStatus.Denied],
                history.Select(h => h.ToStatus));
            for (var i = 1; i < history.Count; i++)
                Assert.Equal(history[i - 1].ToStatus, history[i].FromStatus);
        }

        private static RentalApplication ReturnedApplication()
        {
            var application = NewSubmitted();
            application.Return("manager-1", "Please fix", Now.AddDays(2));
            return application;
        }
    }
}
