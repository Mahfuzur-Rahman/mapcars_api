using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("trips");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.PickupAddress).IsRequired().HasMaxLength(500);
        builder.Property(t => t.DropoffAddress).IsRequired().HasMaxLength(500);

        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.FareAmount).HasPrecision(10, 2);

        builder.HasOne(t => t.Rider)
            .WithMany(r => r.Trips)
            .HasForeignKey(t => t.RiderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Driver)
            .WithMany(d => d.Trips)
            .HasForeignKey(t => t.DriverId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.Status);
    }
}
