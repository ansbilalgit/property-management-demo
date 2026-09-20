using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Infrastructure.Data;
using Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services
{
    public interface IApplicationService
    {
        Task<int> StartAsync(string applicantId, int unitId, CancellationToken cancellationToken = default);
        Task<ApplicationDto?> GetByIdAsync(int id, string applicantId, CancellationToken cancellationToken = default);
        Task<List<ApplicationDto>> GetApplicantApplicationsAsync(string applicantId, ApplicationListFilterDto filter, CancellationToken cancellationToken = default);
        Task<List<ApplicationDto>> GetAllApplicationsAsync(ApplicationListFilterDto filter, CancellationToken cancellationToken = default);
        Task SaveApplicantInfoAsync(string applicantId, ApplicantInfoInputDto input, CancellationToken cancellationToken = default);
        Task SaveResidenceAsync(string applicantId, ResidenceInputDto input, CancellationToken cancellationToken = default);
        Task DeleteResidenceAsync(string applicantId, int applicationId, int residenceId, CancellationToken cancellationToken = default);
        Task CompleteResidenceHistoryAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default);
        Task SubmitAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default);
        Task WithdrawAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default);
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

            var now = DateTime.UtcNow;
            var application = new RentalApplication
            {
                UnitId = unitId,
                ApplicantId = applicantId,
                Status = ApplicationStatus.Draft,
                CreatedAt = now,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Phone = user.PhoneNumber ?? string.Empty,
                CurrentAddress = user.CurrentAddress ?? string.Empty
            };

            application.StatusHistory.Add(new ApplicationStatusHistory
            {
                FromStatus = null,
                ToStatus = ApplicationStatus.Draft,
                ChangedById = applicantId,
                ChangedAt = now
            });

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

        public Task<List<ApplicationDto>> GetApplicantApplicationsAsync(string applicantId, ApplicationListFilterDto filter, CancellationToken cancellationToken = default) =>
            ApplyFilter(_db.RentalApplications.AsNoTracking().Where(a => a.ApplicantId == applicantId), filter)
                .OrderByDescending(a => a.CreatedAt)
                .ProjectTo<ApplicationDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

        public Task<List<ApplicationDto>> GetAllApplicationsAsync(ApplicationListFilterDto filter, CancellationToken cancellationToken = default) =>
            ApplyFilter(_db.RentalApplications.AsNoTracking(), filter)
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
            var application = await GetEditableAsync(input.ApplicationId, applicantId, cancellationToken);

            application.FullName = input.FullName.Trim();
            application.Phone = input.Phone.Trim();
            application.Email = input.Email.Trim();
            application.CurrentAddress = input.CurrentAddress.Trim();
            application.ApplicantInfoSaved = true;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveResidenceAsync(string applicantId, ResidenceInputDto input, CancellationToken cancellationToken = default)
        {
            var application = await GetEditableAsync(input.ApplicationId, applicantId, cancellationToken);

            if (input.MoveOutDate < input.MoveInDate)
                throw new BusinessRuleException(nameof(ResidenceInputDto.MoveOutDate), "Move-out date cannot be before the move-in date.");

            Residence residence;
            if (input.Id is { } id)
            {
                residence = application.Residences.FirstOrDefault(r => r.Id == id)
                    ?? throw new BusinessRuleException(string.Empty, "The residence no longer exists.");
            }
            else
            {
                residence = new Residence();
                application.Residences.Add(residence);
            }

            residence.Address = input.Address.Trim();
            residence.LandlordName = input.LandlordName.Trim();
            residence.LandlordPhone = input.LandlordPhone.Trim();
            residence.MoveInDate = input.MoveInDate;
            residence.MoveOutDate = input.MoveOutDate;

            // A changed list has to be confirmed again with Continue.
            application.ResidenceHistorySaved = false;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteResidenceAsync(string applicantId, int applicationId, int residenceId, CancellationToken cancellationToken = default)
        {
            var application = await GetEditableAsync(applicationId, applicantId, cancellationToken);

            var residence = application.Residences.FirstOrDefault(r => r.Id == residenceId);
            if (residence is null)
                return;

            application.Residences.Remove(residence);
            _db.Residences.Remove(residence);
            application.ResidenceHistorySaved = false;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task CompleteResidenceHistoryAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default)
        {
            var application = await GetEditableAsync(applicationId, applicantId, cancellationToken);

            application.ResidenceHistorySaved = true;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SubmitAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default)
        {
            var application = await GetOwnedAsync(applicationId, applicantId, cancellationToken);

            if (application.Status is not (ApplicationStatus.Draft or ApplicationStatus.Returned))
                throw new BusinessRuleException(string.Empty, "Only draft or returned applications can be submitted.");

            if (!application.ApplicantInfoSaved || !application.ResidenceHistorySaved)
                throw new BusinessRuleException(string.Empty, "Save both sections before submitting.");

            var now = DateTime.UtcNow;
            ChangeStatus(application, ApplicationStatus.Submitted, applicantId, now);
            application.SubmittedAt = now;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task WithdrawAsync(string applicantId, int applicationId, CancellationToken cancellationToken = default)
        {
            var application = await GetOwnedAsync(applicationId, applicantId, cancellationToken);

            if (application.Status is not (ApplicationStatus.Draft or ApplicationStatus.Submitted or ApplicationStatus.Returned))
                throw new BusinessRuleException(string.Empty, "This application can no longer be withdrawn.");

            ChangeStatus(application, ApplicationStatus.Withdrawn, applicantId, DateTime.UtcNow);

            await _db.SaveChangesAsync(cancellationToken);
        }

        // Loads an application the applicant owns, tracked, with its residences.
        private async Task<RentalApplication> GetOwnedAsync(int applicationId, string applicantId, CancellationToken cancellationToken) =>
            await _db.RentalApplications
                .Include(a => a.Residences)
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.ApplicantId == applicantId, cancellationToken)
                ?? throw new BusinessRuleException(string.Empty, "The application was not found.");

        // Same as GetOwnedAsync, but only while the sections may still be edited.
        private async Task<RentalApplication> GetEditableAsync(int applicationId, string applicantId, CancellationToken cancellationToken)
        {
            var application = await GetOwnedAsync(applicationId, applicantId, cancellationToken);

            if (application.Status is not (ApplicationStatus.Draft or ApplicationStatus.Returned))
                throw new BusinessRuleException(string.Empty, "This application can no longer be edited.");

            return application;
        }

        private static void ChangeStatus(RentalApplication application, ApplicationStatus to, string changedById, DateTime at, string? comment = null)
        {
            application.StatusHistory.Add(new ApplicationStatusHistory
            {
                FromStatus = application.Status,
                ToStatus = to,
                ChangedById = changedById,
                ChangedAt = at,
                Comment = comment
            });
            application.Status = to;
        }
    }
}
