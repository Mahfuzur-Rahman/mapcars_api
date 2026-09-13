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
        builder.Property(t => t.Pin).HasMaxLength(4);
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
        // above stayed PascalCase, unlike customers/drivers).
        builder.Property(t => t.CancelledReason).HasMaxLength(500);

        // The ONLY mapping in this file EF was deriving by convention, and the one
        // that made the C# rename and the database rename inseparable: with
        // Trip.RiderId the convention produced "RiderId" by accident, so renaming
        // the property silently retargeted EF at a "CustomerId" column that does
        // not exist until 031. It compiles and then fails on every trip query.
        // Pinned explicitly so the two can never drift again; the literal flips to
        // "CustomerId" in the same commit as the migration.
        builder.Property(t => t.CustomerId).HasColumnName("CustomerId");

        builder.HasOne(t => t.Customer)
            .WithMany(r => r.Trips)
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Driver)
            .WithMany(d => d.Trips)
            .HasForeignKey(t => t.DriverId)
            .OnDelete(DeleteBehavior.SetNull);

        // Search window (029_trip_expiry.sql). The column carries a DB-level
        // default so the script can be applied before this image is deployed
        // (see the script) — but the deadline is always set explicitly in
        // TripService.CreateAsync, and that is where it belongs.
        builder.Property(t => t.ExpiresAtUtc).IsRequired();
        builder.Property(t => t.ExtensionCount).IsRequired().HasDefaultValue(0);

        builder.HasIndex(t => t.Status);

        // Serves both the board queries and the lifecycle sweeper, which ask the
        // same question from opposite ends: open requests whose window is still,
        // or no longer, live.
        builder.HasIndex(t => new { t.Status, t.ExpiresAtUtc })
            .HasDatabaseName("ix_trips_status_expires");
    }
}
