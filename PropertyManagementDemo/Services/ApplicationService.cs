using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Infrastructure.Data;
using Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Services
{
    public interface IApplicationService
    {
        Task<int> StartAsync(string applicantId, int unitId, CancellationToken cancellationToken = default);
        Task<ApplicationDto?> GetByIdAsync(int id, string applicantId, CancellationToken cancellationToken = default);
        Task<ApplicationDto?> GetForReviewAsync(int id, CancellationToken cancellationToken = default);
        Task<List<ApplicationDto>> GetApplicantApplicationsAsync(string applicantId, ApplicationListFilterDto filter, CancellationToken cancellationToken = default);
        Task<List<ApplicationDto>> GetAllApplicationsAsync(ApplicationListFilterDto filter, CancellationToken cancellationToken = default);
        Task SaveApplicantInfoAsync(string applicantId, ApplicantInfoInputDto input, CancellationToken cancellationToken = default);
        Task SaveResidenceAsync(string applicantId, ResidenceInputDto input, CancellationToken cancellationToken = default);
        Task DeleteResidenceAsync(string applicantId, int applicationId, int residenceId, CancellationToken cancellationToken = default);
        Task CompleteResidenceHistoryAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default);
        Task SubmitAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default);
        Task WithdrawAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default);
        Task ReviewAsync(string managerId, ReviewInputDto input, CancellationToken cancellationToken = default);
    }

    public class ApplicationService : IApplicationService
    {
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;

        public ApplicationService(AppDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        public async Task<int> StartAsync(string applicantId, int unitId, CancellationToken cancellationToken = default)
        {
            var unitExists = await _db.Units.AnyAsync(u => u.Id == unitId, cancellationToken);
            if (!unitExists)
                throw new BusinessRuleException(string.Empty, "The unit no longer exists.");

            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == applicantId, cancellationToken)
                ?? throw new BusinessRuleException(string.Empty, "The applicant account no longer exists.");

            var application = RentalApplication.Start(
                unitId,
                applicantId,
                user.FullName,
                user.Email ?? string.Empty,
                user.PhoneNumber ?? string.Empty,
                user.CurrentAddress ?? string.Empty,
                DateTime.UtcNow);

            _db.RentalApplications.Add(application);
            await _db.SaveChangesAsync(cancellationToken);

            return application.Id;
        }

        public async Task<ApplicationDto?> GetByIdAsync(int id, string applicantId, CancellationToken cancellationToken = default)
        {
            var application = await _db.RentalApplications.AsNoTracking()
                .Where(a => a.Id == id && a.ApplicantId == applicantId)
                .ProjectTo<ApplicationDto>(_mapper.ConfigurationProvider, a => a.Residences)
                .FirstOrDefaultAsync(cancellationToken);

            application?.Residences.Sort((a, b) => a.MoveInDate.CompareTo(b.MoveInDate));
            return application;
        }

        // For property managers: any application that was ever submitted, with its residences and status history.
        // Drafts belong to the applicant until submitted, so they are not returned. No ownership check.
        public async Task<ApplicationDto?> GetForReviewAsync(int id, CancellationToken cancellationToken = default)
        {
            var application = await _db.RentalApplications.AsNoTracking()
                .Where(a => a.Id == id && a.SubmittedAt != null)
                .ProjectTo<ApplicationDto>(_mapper.ConfigurationProvider, a => a.Residences, a => a.History)
                .FirstOrDefaultAsync(cancellationToken);

            application?.Residences.Sort((a, b) => a.MoveInDate.CompareTo(b.MoveInDate));
            application?.History.Sort((a, b) => a.ChangedAt.CompareTo(b.ChangedAt));
            return application;
        }

        public Task<List<ApplicationDto>> GetApplicantApplicationsAsync(string applicantId, ApplicationListFilterDto filter, CancellationToken cancellationToken = default) =>
            ApplyFilter(_db.RentalApplications.AsNoTracking().Where(a => a.ApplicantId == applicantId), filter)
                .OrderByDescending(a => a.CreatedAt)
                .ProjectTo<ApplicationDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

        public Task<List<ApplicationDto>> GetAllApplicationsAsync(ApplicationListFilterDto filter, CancellationToken cancellationToken = default) =>
            ApplyFilter(_db.RentalApplications.AsNoTracking().Where(a => a.SubmittedAt != null), filter)
                .OrderByDescending(a => a.CreatedAt)
                .ProjectTo<ApplicationDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

        // Both filters are translated to SQL; nothing is filtered in memory.
        private static IQueryable<RentalApplication> ApplyFilter(IQueryable<RentalApplication> applications, ApplicationListFilterDto filter)
        {
            if (filter.Status is { } status)
                applications = applications.Where(a => a.Status == status);

            if (filter.PropertyId is { } propertyId)
                applications = applications.Where(a => a.Unit.PropertyId == propertyId);

            return applications;
        }

        public async Task SaveApplicantInfoAsync(string applicantId, ApplicantInfoInputDto input, CancellationToken cancellationToken = default)
        {
            var application = await GetOwnedAsync(input.ApplicationId, applicantId, cancellationToken);

            application.SaveApplicantInfo(input.FullName, input.Phone, input.Email, input.CurrentAddress);

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveResidenceAsync(string applicantId, ResidenceInputDto input, CancellationToken cancellationToken = default)
        {
            var application = await GetOwnedAsync(input.ApplicationId, applicantId, cancellationToken);

            if (input.Id is { } id)
                application.UpdateResidence(id, input.Address, input.LandlordName, input.LandlordPhone, input.MoveInDate, input.MoveOutDate);
            else
                application.AddResidence(input.Address, input.LandlordName, input.LandlordPhone, input.MoveInDate, input.MoveOutDate);

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteResidenceAsync(string applicantId, int applicationId, int residenceId, CancellationToken cancellationToken = default)
        {
            var application = await GetOwnedAsync(applicationId, applicantId, cancellationToken);

            var removed = application.RemoveResidence(residenceId);
            if (removed is null)
                return;

            _db.Residences.Remove(removed);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task CompleteResidenceHistoryAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default)
        {
            var application = await GetOwnedAsync(applicationId, applicantId, cancellationToken);

            application.CompleteResidenceHistory();

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SubmitAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default)
        {
            var application = await GetOwnedAsync(applicationId, applicantId, cancellationToken);

            await EnsureUnitHasNoActiveLeaseAsync(application.UnitId, cancellationToken);

            application.Submit(applicantId, DateTime.UtcNow);

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task WithdrawAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default)
        {
            var application = await GetOwnedAsync(applicationId, applicantId, cancellationToken);

            application.Withdraw(applicantId, DateTime.UtcNow);

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ReviewAsync(string managerId, ReviewInputDto input, CancellationToken cancellationToken = default)
        {
            var application = await _db.RentalApplications
                .FirstOrDefaultAsync(a => a.Id == input.ApplicationId, cancellationToken)
                ?? throw new BusinessRuleException(string.Empty, "The application was not found.");

            var now = DateTime.UtcNow;

            switch (input.Outcome)
            {
                case ReviewOutcome.Approve:
                    // The unit may have been leased since this application was submitted.
                    await EnsureUnitHasNoActiveLeaseAsync(application.UnitId, cancellationToken);
                    var lease = application.Approve(managerId, input.Comment, DateOnly.FromDateTime(DateTime.Today), now);
                    _db.Leases.Add(lease);
                    break;

                case ReviewOutcome.Return:
                    application.Return(managerId, input.Comment, now);
                    break;

                case ReviewOutcome.Deny:
                    application.Deny(managerId, input.Comment, now);
                    break;

                default:
                    throw new BusinessRuleException(nameof(ReviewInputDto.Outcome), "Select an outcome.");
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task EnsureUnitHasNoActiveLeaseAsync(int unitId, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var hasActiveLease = await _db.Leases.AnyAsync(
                l => l.UnitId == unitId && l.StartDate <= today && today <= l.EndDate, cancellationToken);

            if (hasActiveLease)
                throw new BusinessRuleException(string.Empty, "This unit already has an active lease.");
        }

        // Loads an application the applicant owns, tracked, with its residences.
        private async Task<RentalApplication> GetOwnedAsync(int applicationId, string applicantId, CancellationToken cancellationToken) =>
            await _db.RentalApplications
                .Include(a => a.Residences)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.ApplicantId == applicantId, cancellationToken)
                ?? throw new BusinessRuleException(string.Empty, "The application was not found.");

    }
}
