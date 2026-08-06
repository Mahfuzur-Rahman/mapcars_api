using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class SavedPlaceConfiguration : IEntityTypeConfiguration<SavedPlace>
{
    public void Configure(EntityTypeBuilder<SavedPlace> b)
    {
        b.ToTable("saved_places");
        b.HasKey(p => p.Id);

        // snake_case descriptive columns, PascalCase base columns — see database/012_saved_places.sql.
        b.Property(p => p.RiderId).HasColumnName("rider_id");
        b.Property(p => p.Label).HasColumnName("label").IsRequired().HasMaxLength(40);
        b.Property(p => p.Address).HasColumnName("address").IsRequired().HasMaxLength(500);
        b.Property(p => p.Lat).HasColumnName("lat");
        b.Property(p => p.Lng).HasColumnName("lng");

        b.HasOne(p => p.Rider)
            .WithMany(r => r.SavedPlaces)
            .HasForeignKey(p => p.RiderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Many places per rider, but not two with the same label.
        b.HasIndex(p => p.RiderId);
        b.HasIndex(p => new { p.RiderId, p.Label }).IsUnique();
    }
}
