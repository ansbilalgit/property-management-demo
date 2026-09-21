using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations
{
    public class LeaseConfiguration : IEntityTypeConfiguration<Lease>
    {
        public void Configure(EntityTypeBuilder<Lease> builder)
        {
            // One application can produce at most one lease, even if two approvals race.
            builder.HasIndex(l => l.RentalApplicationId).IsUnique();
            builder.HasIndex(l => new { l.UnitId, l.StartDate, l.EndDate });

            builder.HasOne(l => l.Unit)
                .WithMany(u => u.Leases)
                .HasForeignKey(l => l.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(l => l.Applicant)
                .WithMany()
                .HasForeignKey(l => l.ApplicantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(l => l.RentalApplication)
                .WithOne()
                .HasForeignKey<Lease>(l => l.RentalApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
