using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Data;
using Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Services
{
    public interface IUnitService
    {
        Task<List<UnitDto>> GetUnitsByPropertyAsync(int propertyId, CancellationToken cancellationToken = default);
        Task<List<UnitDto>> GetAvailableUnitsAsync(CancellationToken cancellationToken = default);
        Task<UnitDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<UnitTypeDto>> GetSelectableUnitTypesAsync(int? currentUnitTypeId, CancellationToken cancellationToken = default);
        Task SaveAsync(UnitInputDto input, CancellationToken cancellationToken = default);
        Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    }

    public class UnitService : IUnitService
    {
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;

        public UnitService(AppDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

        public Task<List<UnitDto>> GetUnitsByPropertyAsync(int propertyId, CancellationToken cancellationToken = default) =>
            _db.Units.AsNoTracking()
                .Where(u => u.PropertyId == propertyId)
                .OrderBy(u => u.UnitNumber)
                .ProjectTo<UnitDto>(_mapper.ConfigurationProvider, new { today = Today })
                .ToListAsync(cancellationToken);

        // A unit whose lease term covers today is not available.
        public Task<List<UnitDto>> GetAvailableUnitsAsync(CancellationToken cancellationToken = default)
        {
            var today = Today;
            return _db.Units.AsNoTracking()
                .Where(u => !u.Leases.Any(l => l.StartDate <= today && today <= l.EndDate))
                .OrderBy(u => u.Property.Name).ThenBy(u => u.UnitNumber)
                .ProjectTo<UnitDto>(_mapper.ConfigurationProvider, new { today })
                .ToListAsync(cancellationToken);
        }

        public Task<UnitDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            _db.Units.AsNoTracking()
                .Where(u => u.Id == id)
                .ProjectTo<UnitDto>(_mapper.ConfigurationProvider, new { today = Today })
                .FirstOrDefaultAsync(cancellationToken);

        public Task<List<UnitTypeDto>> GetSelectableUnitTypesAsync(int? currentUnitTypeId, CancellationToken cancellationToken = default) =>
            _db.UnitTypes.AsNoTracking()
                .Where(t => t.IsActive || t.Id == currentUnitTypeId)
                .OrderBy(t => t.Name)
                .ProjectTo<UnitTypeDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

        public async Task SaveAsync(UnitInputDto input, CancellationToken cancellationToken = default)
        {
            var propertyExists = await _db.Properties.AnyAsync(p => p.Id == input.PropertyId, cancellationToken);
            if (!propertyExists)
                throw new BusinessRuleException(string.Empty, "The property no longer exists.");

            var unitType = await _db.UnitTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == input.UnitTypeId, cancellationToken);
            if (unitType is null)
                throw new BusinessRuleException(nameof(UnitInputDto.UnitTypeId), "Select a valid unit type.");

            Unit? unit = null;
            if (input.Id is { } id)
            {
                unit = await _db.Units.FindAsync([id], cancellationToken);
                if (unit is null || unit.PropertyId != input.PropertyId)
                    throw new BusinessRuleException(string.Empty, "The unit no longer exists.");
            }

            // An inactive type may stay on a unit that already uses it, but cannot be newly assigned.
            var isKeepingCurrentType = unit is not null && unit.UnitTypeId == input.UnitTypeId;
            if (!unitType.IsActive && !isKeepingCurrentType)
                throw new BusinessRuleException(nameof(UnitInputDto.UnitTypeId), "This unit type is inactive and cannot be selected.");

            var unitNumber = input.UnitNumber.Trim();
            var duplicate = await _db.Units.AnyAsync(u =>
                u.PropertyId == input.PropertyId && u.UnitNumber == unitNumber && u.Id != input.Id, cancellationToken);
            if (duplicate)
                throw new BusinessRuleException(nameof(UnitInputDto.UnitNumber), "This property already has a unit with that number.");

            if (unit is null)
            {
                unit = new Unit { PropertyId = input.PropertyId };
                _db.Units.Add(unit);
            }

            unit.UnitNumber = unitNumber;
            unit.Bedrooms = input.Bedrooms;
            unit.MonthlyRent = input.MonthlyRent;
            unit.UnitTypeId = input.UnitTypeId;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var unit = await _db.Units.FindAsync([id], cancellationToken);
            if (unit is null)
                return;

            var hasLeases = await _db.Leases.AnyAsync(l => l.UnitId == id, cancellationToken);
            var hasApplications = await _db.RentalApplications.AnyAsync(a => a.UnitId == id, cancellationToken);
            if (hasLeases || hasApplications)
                throw new BusinessRuleException(string.Empty, "This unit has applications or lease history and cannot be removed.");

            _db.Units.Remove(unit);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
