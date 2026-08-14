using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);

        // snake_case, matching database/025_refresh_tokens.sql and the other
        // auth-side tables (verification_codes, device_tokens).
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.UserId).HasColumnName("user_id");
        builder.Property(t => t.UserType).HasColumnName("user_type").IsRequired().HasMaxLength(20);
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").IsRequired().HasMaxLength(128);
        builder.Property(t => t.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(t => t.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.Property(t => t.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash").HasMaxLength(128);
        builder.Property(t => t.DeviceLabel).HasColumnName("device_label").HasMaxLength(120);
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(t => t.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Computed on the entity, not stored.
        builder.Ignore(t => t.IsExpired);
        builder.Ignore(t => t.IsActive);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.UserType });
    }
}
