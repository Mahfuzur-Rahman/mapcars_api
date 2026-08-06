using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_tokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.UserType).HasColumnName("user_type").IsRequired().HasMaxLength(10);
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.Token).HasColumnName("token").IsRequired().HasMaxLength(512);
        builder.Property(t => t.Platform).HasColumnName("platform").HasMaxLength(10);
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // One row per FCM token (upserted); look-ups are by owner.
        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => new { t.UserType, t.UserId });
    }
}
