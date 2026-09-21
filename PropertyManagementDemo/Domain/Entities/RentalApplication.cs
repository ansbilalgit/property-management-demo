using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities
{
    public class RentalApplication
    {
        private readonly List<Residence> _residences = new();
        private readonly List<ApplicationStatusHistory> _statusHistory = new();

        private RentalApplication()
        {
        }

        public int Id { get; private set; }

        public int UnitId { get; private set; }
        public Unit Unit { get; private set; } = null!;

        public string ApplicantId { get; private set; } = string.Empty;
        public ApplicationUser Applicant { get; private set; } = null!;

        public ApplicationStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? SubmittedAt { get; private set; }

        // Section 1: Applicant Information
        public string FullName { get; private set; } = string.Empty;
        public string Phone { get; private set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;
        public string CurrentAddress { get; private set; } = string.Empty;
        public bool ApplicantInfoSaved { get; private set; }

        // Section 2: Residence History
        public bool ResidenceHistorySaved { get; private set; }
        public IReadOnlyCollection<Residence> Residences => _residences;

        public IReadOnlyCollection<ApplicationStatusHistory> StatusHistory => _statusHistory;

        // An applicant can edit while the application is a Draft or Returned; otherwise every section is read-only.
        public bool IsEditable => Status is ApplicationStatus.Draft or ApplicationStatus.Returned;

        public static RentalApplication Start(
            int unitId, string applicantId, string fullName, string email, string phone, string currentAddress, DateTime now)
        {
            var application = new RentalApplication
            {
                UnitId = unitId,
                ApplicantId = applicantId,
                Status = ApplicationStatus.Draft,
                CreatedAt = now,
                FullName = fullName,
                Email = email,
                Phone = phone,
                CurrentAddress = currentAddress
            };

            application.RecordStatus(null, ApplicationStatus.Draft, applicantId, now, null);
            return application;
        }

        public void SaveApplicantInfo(string fullName, string phone, string email, string currentAddress)
        {
            EnsureEditable();

            FullName = fullName.Trim();
            Phone = phone.Trim();
            Email = email.Trim();
            CurrentAddress = currentAddress.Trim();
            ApplicantInfoSaved = true;
        }

        public void AddResidence(string address, string landlordName, string landlordPhone, DateOnly moveIn, DateOnly moveOut)
        {
            EnsureEditable();

            _residences.Add(new Residence(address, landlordName, landlordPhone, moveIn, moveOut));

            // A changed list has to be confirmed again with Continue.
            ResidenceHistorySaved = false;
        }

        public void UpdateResidence(int residenceId, string address, string landlordName, string landlordPhone, DateOnly moveIn, DateOnly moveOut)
        {
            EnsureEditable();

            var residence = _residences.FirstOrDefault(r => r.Id == residenceId)
                ?? throw new BusinessRuleException(string.Empty, "The residence no longer exists.");

            residence.Update(address, landlordName, landlordPhone, moveIn, moveOut);
            ResidenceHistorySaved = false;
        }

        // Returns the removed residence so the caller can delete it from the database, or null if it was already gone.
        public Residence? RemoveResidence(int residenceId)
        {
            EnsureEditable();

            var residence = _residences.FirstOrDefault(r => r.Id == residenceId);
            if (residence is null)
                return null;

            _residences.Remove(residence);
            ResidenceHistorySaved = false;
            return residence;
        }

        public void CompleteResidenceHistory()
        {
            EnsureEditable();
            ResidenceHistorySaved = true;
        }

        public void Submit(string changedById, DateTime now)
        {
            if (!IsEditable)
                throw new BusinessRuleException(string.Empty, "Only draft or returned applications can be submitted.");

            if (!ApplicantInfoSaved || !ResidenceHistorySaved)
                throw new BusinessRuleException(string.Empty, "Save both sections before submitting.");

            ChangeStatus(ApplicationStatus.Submitted, changedById, now, null);
            SubmittedAt = now;
        }

        public void Withdraw(string changedById, DateTime now)
        {
            if (Status is not (ApplicationStatus.Draft or ApplicationStatus.Submitted or ApplicationStatus.Returned))
                throw new BusinessRuleException(string.Empty, "This application can no longer be withdrawn.");

            ChangeStatus(ApplicationStatus.Withdrawn, changedById, now, null);
        }

        // Approval issues a twelve-month lease. The caller checks for an active lease first, since that needs the database.
        public Lease Approve(string managerId, string? comment, DateOnly leaseStart, DateTime now)
        {
            EnsureReviewable();

            ChangeStatus(ApplicationStatus.Approved, managerId, now, NormalizeComment(comment));
            return Lease.ForTwelveMonths(this, leaseStart);
        }

        public void Return(string managerId, string? comment, DateTime now)
        {
            EnsureReviewable();
            ChangeStatus(ApplicationStatus.Returned, managerId, now, RequireComment(comment));
        }

        public void Deny(string managerId, string? comment, DateTime now)
        {
            EnsureReviewable();
            ChangeStatus(ApplicationStatus.Denied, managerId, now, RequireComment(comment));
        }

        private void EnsureEditable()
        {
            if (!IsEditable)
                throw new BusinessRuleException(string.Empty, "This application can no longer be edited.");
        }

        private void EnsureReviewable()
        {
            if (Status != ApplicationStatus.Submitted)
                throw new BusinessRuleException(string.Empty, "Only submitted applications can be reviewed.");
        }

        private static string? NormalizeComment(string? comment) =>
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        private static string RequireComment(string? comment) =>
            NormalizeComment(comment)
            ?? throw new BusinessRuleException("Comment", "A comment is required to return or deny an application.");

        private void ChangeStatus(ApplicationStatus to, string changedById, DateTime at, string? comment)
        {
            RecordStatus(Status, to, changedById, at, comment);
            Status = to;
        }

        private void RecordStatus(ApplicationStatus? from, ApplicationStatus to, string changedById, DateTime at, string? comment)
        {
            _statusHistory.Add(new ApplicationStatusHistory(from, to, changedById, at, comment));
        }
    }
}
