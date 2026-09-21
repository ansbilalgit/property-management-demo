using Microsoft.Extensions.DependencyInjection;
using Services.Mapping;

namespace Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            services.AddAutoMapper(cfg => cfg.AddMaps(typeof(MappingProfile).Assembly));

            services.AddScoped<IPropertyService, PropertyService>();
            services.AddScoped<IUnitService, UnitService>();
            services.AddScoped<IApplicationService, ApplicationService>();

            return services;
        }
    }
}
