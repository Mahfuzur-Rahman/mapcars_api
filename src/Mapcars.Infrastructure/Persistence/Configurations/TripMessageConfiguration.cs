using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class TripMessageConfiguration : IEntityTypeConfiguration<TripMessage>
{
    public void Configure(EntityTypeBuilder<TripMessage> b)
    {
        b.ToTable("trip_messages");
        b.HasKey(m => m.Id);

        // snake_case feature columns, PascalCase base columns — see database/026_trip_messages.sql.
        b.Property(m => m.TripId).HasColumnName("trip_id");
        b.Property(m => m.SenderType).HasColumnName("sender_type").IsRequired().HasMaxLength(10);
        b.Property(m => m.SenderId).HasColumnName("sender_id");
        b.Property(m => m.Content).HasColumnName("content").IsRequired();
        b.Property(m => m.SentAtUtc).HasColumnName("sent_at_utc");

        b.HasOne(m => m.Trip)
            .WithMany()
            .HasForeignKey(m => m.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(m => new { m.TripId, m.SentAtUtc });
    }
}
