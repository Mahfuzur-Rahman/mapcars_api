using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class VehicleTierAppealConfiguration : IEntityTypeConfiguration<VehicleTierAppeal>
{
    public void Configure(EntityTypeBuilder<VehicleTierAppeal> b)
    {
        b.ToTable("vehicle_tier_appeals");
        b.HasKey(a => a.Id);

        b.Property(a => a.DriverId).HasColumnName("driver_id").IsRequired();
        b.Property(a => a.VehicleId).HasColumnName("vehicle_id").IsRequired();
        b.Property(a => a.CurrentTier).HasColumnName("current_tier").IsRequired().HasMaxLength(30);
        b.Property(a => a.RequestedTier).HasColumnName("requested_tier").IsRequired().HasMaxLength(30);
        b.Property(a => a.Reason).HasColumnName("reason").IsRequired();
        b.Property(a => a.PhotoStorageKeys).HasColumnName("photo_storage_keys");
        b.Property(a => a.Status).HasColumnName("status").HasConversion<string>().IsRequired().HasMaxLength(30);
        b.Property(a => a.AdminNotes).HasColumnName("admin_notes");
        b.Property(a => a.ReviewedByAdminId).HasColumnName("reviewed_by_admin_id");
        b.Property(a => a.ReviewedAtUtc).HasColumnName("reviewed_at_utc");

        b.HasOne(a => a.Driver)
            .WithMany()
            .HasForeignKey(a => a.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(a => a.Vehicle)
            .WithMany()
            .HasForeignKey(a => a.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(a => a.ReviewedByAdmin)
            .WithMany()
            .HasForeignKey(a => a.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(a => a.DriverId);
        b.HasIndex(a => a.VehicleId);
        b.HasIndex(a => a.Status);
    }
}
