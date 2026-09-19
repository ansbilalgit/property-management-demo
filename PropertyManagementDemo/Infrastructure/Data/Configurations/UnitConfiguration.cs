using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public class UnitConfiguration : IEntityTypeConfiguration<Unit>
    {
        public void Configure(EntityTypeBuilder<Unit> builder)
        {
            builder.Property(u => u.UnitNumber).HasMaxLength(20).IsRequired();
            builder.Property(u => u.MonthlyRent).HasPrecision(10, 2);
            builder.HasIndex(u => new { u.PropertyId, u.UnitNumber }).IsUnique();

            builder.HasOne(u => u.UnitType)
                .WithMany()
                .HasForeignKey(u => u.UnitTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
