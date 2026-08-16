using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.ToTable("vehicles");
        b.HasKey(v => v.Id);

        // snake_case descriptive columns, PascalCase base columns — see database/010_vehicles.sql.
        b.Property(v => v.DriverId).HasColumnName("driver_id");
        b.Property(v => v.Make).HasColumnName("make").IsRequired().HasMaxLength(60);
        b.Property(v => v.Model).HasColumnName("model").IsRequired().HasMaxLength(60);
        b.Property(v => v.Year).HasColumnName("year");
        b.Property(v => v.Colour).HasColumnName("colour").IsRequired().HasMaxLength(40);
        b.Property(v => v.RegistrationNumber).HasColumnName("registration_number").IsRequired().HasMaxLength(15);
        b.Property(v => v.PhvLicencePlateNumber).HasColumnName("phv_licence_plate_number").HasMaxLength(30);
        b.Property(v => v.PhvLicensingAuthority).HasColumnName("phv_licensing_authority").HasMaxLength(120);
        b.Property(v => v.Tier).HasColumnName("tier").IsRequired().HasMaxLength(30).HasDefaultValue("economy");

        b.HasOne(v => v.Driver)
            .WithMany()
            .HasForeignKey(v => v.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        // One vehicle per driver, and registration plates are unique platform-wide.
        b.HasIndex(v => v.DriverId).IsUnique();
        b.HasIndex(v => v.RegistrationNumber).IsUnique();
        b.HasIndex(v => v.Tier);
    }
}
