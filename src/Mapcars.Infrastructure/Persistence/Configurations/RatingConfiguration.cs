using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> b)
    {
        b.ToTable("ratings");
        b.HasKey(r => r.Id);

        // snake_case descriptive columns, PascalCase base columns — see database/014_ratings.sql.
        b.Property(r => r.TripId).HasColumnName("trip_id");
        b.Property(r => r.RaterType).HasColumnName("rater_type").IsRequired().HasMaxLength(10);
        b.Property(r => r.Score).HasColumnName("score");
        b.Property(r => r.Comment).HasColumnName("comment").HasMaxLength(1000);

        b.HasOne(r => r.Trip)
            .WithMany()
            .HasForeignKey(r => r.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        // One rating per direction per trip.
        b.HasIndex(r => new { r.TripId, r.RaterType }).IsUnique();
    }
}
