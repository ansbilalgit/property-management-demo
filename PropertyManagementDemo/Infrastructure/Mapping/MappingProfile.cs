using AutoMapper;
using Domain.Entities;
using Infrastructure.Dtos;

namespace Infrastructure.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Property, PropertyDto>()
                .ForMember(d => d.UnitCount, o => o.MapFrom(s => s.Units.Count));

            // PropertyName, UnitTypeName and UnitTypeIsActive are flattened by convention.
            CreateMap<Unit, UnitDto>();

            CreateMap<UnitType, UnitTypeDto>();

            CreateMap<Residence, ResidenceDto>();

            CreateMap<RentalApplication, ApplicationDto>()
                .ForMember(d => d.PropertyName, o => o.MapFrom(s => s.Unit.Property.Name))
                .ForMember(d => d.UnitNumber, o => o.MapFrom(s => s.Unit.UnitNumber))
                .ForMember(d => d.MonthlyRent, o => o.MapFrom(s => s.Unit.MonthlyRent));
        }
    }
}
