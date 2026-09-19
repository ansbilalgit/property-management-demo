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
        }
    }
}
