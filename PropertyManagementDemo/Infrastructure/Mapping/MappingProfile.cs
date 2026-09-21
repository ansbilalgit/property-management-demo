using AutoMapper;
using Domain.Entities;
using Infrastructure.Dtos;

namespace Infrastructure.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Supplied at projection time: ProjectTo<UnitDto>(config, new { today }). Must be passed every time.
            DateOnly today = default;

            CreateMap<Property, PropertyDto>()
                .ForMember(d => d.UnitCount, o => o.MapFrom(s => s.Units.Count));

            // PropertyName, UnitTypeName and UnitTypeIsActive are flattened by convention.
            CreateMap<Unit, UnitDto>()
                .ForMember(d => d.IsAvailable,
                    o => o.MapFrom(s => !s.Leases.Any(l => l.StartDate <= today && today <= l.EndDate)));

            CreateMap<UnitType, UnitTypeDto>();

            CreateMap<Residence, ResidenceDto>();

            CreateMap<ApplicationStatusHistory, StatusHistoryDto>()
                .ForMember(d => d.ChangedByName, o => o.MapFrom(s => s.ChangedBy.FullName));

            CreateMap<RentalApplication, ApplicationDto>()
                .ForMember(d => d.PropertyName, o => o.MapFrom(s => s.Unit.Property.Name))
                .ForMember(d => d.UnitNumber, o => o.MapFrom(s => s.Unit.UnitNumber))
                .ForMember(d => d.MonthlyRent, o => o.MapFrom(s => s.Unit.MonthlyRent))
                .ForMember(d => d.Residences, o => o.ExplicitExpansion())
                .ForMember(d => d.History, o =>
                {
                    o.MapFrom(s => s.StatusHistory);
                    o.ExplicitExpansion();
                });
        }
    }
}
