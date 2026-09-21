using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Data;
using Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services
{
    public interface IPropertyService
    {
        Task<List<PropertyDto>> ListAsync(CancellationToken cancellationToken = default);
        Task<PropertyDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task SaveAsync(PropertyInputDto input, CancellationToken cancellationToken = default);
        Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    }

    public class PropertyService : IPropertyService
    {
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;

        public PropertyService(AppDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        public Task<List<PropertyDto>> ListAsync(CancellationToken cancellationToken = default) =>
            _db.Properties.AsNoTracking()
                .OrderBy(p => p.Name)
                .ProjectTo<PropertyDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

        public Task<PropertyDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            _db.Properties.AsNoTracking()
                .Where(p => p.Id == id)
                .ProjectTo<PropertyDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task SaveAsync(PropertyInputDto input, CancellationToken cancellationToken = default)
        {
            Property? property;
            if (input.Id is { } id)
            {
                property = await _db.Properties.FindAsync([id], cancellationToken);
                if (property is null)
                    throw new BusinessRuleException(string.Empty, "The property no longer exists.");
            }
            else
            {
                property = new Property();
                _db.Properties.Add(property);
            }

            property.Name = input.Name.Trim();
            property.AddressLine = input.AddressLine.Trim();
            property.City = input.City.Trim();
            property.State = input.State.Trim();
            property.PostalCode = input.PostalCode.Trim();

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var property = await _db.Properties.FindAsync([id], cancellationToken);
            if (property is null)
                return;

            var hasLeases = await _db.Leases.AnyAsync(l => l.Unit.PropertyId == id, cancellationToken);
            var hasApplications = await _db.RentalApplications.AnyAsync(a => a.Unit.PropertyId == id, cancellationToken);
            if (hasLeases || hasApplications)
                throw new BusinessRuleException(string.Empty, "This property has units with applications or lease history and cannot be removed.");

            _db.Properties.Remove(property);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
