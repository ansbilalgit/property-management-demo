using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Data;
using Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services
{
    public interface IUnitService
    {
        Task<List<UnitDto>> GetUnitsByPropertyAsync(int propertyId, CancellationToken cancellationToken = default);
        Task<List<UnitDto>> ListAllAsync(CancellationToken cancellationToken = default);
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

        public Task<List<UnitDto>> GetUnitsByPropertyAsync(int propertyId, CancellationToken cancellationToken = default) =>
            _db.Units.AsNoTracking()
                .Where(u => u.PropertyId == propertyId)
                .OrderBy(u => u.UnitNumber)
                .ProjectTo<UnitDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

        public Task<List<UnitDto>> ListAllAsync(CancellationToken cancellationToken = default) =>
            _db.Units.AsNoTracking()
                .OrderBy(u => u.Property.Name).ThenBy(u => u.UnitNumber)
                .ProjectTo<UnitDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

        public Task<UnitDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            _db.Units.AsNoTracking()
                .Where(u => u.Id == id)
                .ProjectTo<UnitDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<List<UnitTypeDto>> GetSelectableUnitTypesAsync(int? currentUnitTypeId, CancellationToken cancellationToken = default) =>
            _db.UnitTypes.AsNoTracking()
                .Where(t => t.IsActive || t.Id == currentUnitTypeId)
                .OrderBy(t => t.Name)
                .ProjectTo<UnitTypeDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

        public async Task SaveAsync(UnitInputDto input, CancellationToken cancellationToken = default)
        {
            if (!await _db.Properties.AnyAsync(p => p.Id == input.PropertyId, cancellationToken))
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

            _db.Units.Remove(unit);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
