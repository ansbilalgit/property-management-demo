using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public class RentalApplicationConfiguration : IEntityTypeConfiguration<RentalApplication>
    {
        public void Configure(EntityTypeBuilder<RentalApplication> builder)
        {
            builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(a => a.FullName).HasMaxLength(250);
            builder.Property(a => a.Phone).HasMaxLength(30);
            builder.Property(a => a.Email).HasMaxLength(256);
            builder.Property(a => a.CurrentAddress).HasMaxLength(250);

            builder.HasIndex(a => a.Status);
            builder.HasIndex(a => new { a.UnitId, a.Status });
            builder.HasIndex(a => a.ApplicantId);

            builder.HasOne(a => a.Unit)
                .WithMany()
                .HasForeignKey(a => a.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.Applicant)
                .WithMany()
                .HasForeignKey(a => a.ApplicantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(a => a.Residences)
                .WithOne(r => r.RentalApplication)
                .HasForeignKey(r => r.RentalApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(a => a.StatusHistory)
                .WithOne(h => h.RentalApplication)
                .HasForeignKey(h => h.RentalApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
