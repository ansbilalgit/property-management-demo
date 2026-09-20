using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public class ResidenceConfiguration : IEntityTypeConfiguration<Residence>
    {
        public void Configure(EntityTypeBuilder<Residence> builder)
        {
            builder.Property(r => r.Address).HasMaxLength(250).IsRequired();
            builder.Property(r => r.LandlordName).HasMaxLength(250).IsRequired();
            builder.Property(r => r.LandlordPhone).HasMaxLength(30).IsRequired();
        }
    }
}
