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
        builder.Property(t => t.TipAmount).HasPrecision(10, 2);

        // Payment (stored as strings, mirroring Status).
        builder.Property(t => t.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.PaymentStatus).HasConversion<string>().HasMaxLength(20);

        // Pricing snapshot (set at booking).
        builder.Property(t => t.Tier).HasMaxLength(30);
        builder.Property(t => t.SurgeMultiplier).HasPrecision(6, 3);
        builder.Property(t => t.PlatformFeeAmount).HasPrecision(10, 2);
        builder.Property(t => t.DriverEarnings).HasPrecision(10, 2);

        // Lifecycle/cancellation — new PascalCase columns, matching this table's
        // existing convention (even later additions like the pricing snapshot
        // above stayed PascalCase, unlike riders/drivers).
        builder.Property(t => t.CancelledReason).HasMaxLength(500);

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
