using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> b)
    {
        b.ToTable("verification_codes");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).HasColumnName("id");
        b.Property(v => v.UserType).HasColumnName("user_type").HasMaxLength(10).IsRequired();
        b.Property(v => v.Provider).HasColumnName("provider").HasMaxLength(10).IsRequired();
        b.Property(v => v.Identifier).HasColumnName("identifier").HasMaxLength(255).IsRequired();
        b.Property(v => v.Code).HasColumnName("code").HasMaxLength(6).IsRequired();
        b.Property(v => v.ExpiresAt).HasColumnName("expires_at");
        b.Property(v => v.UsedAt).HasColumnName("used_at");
        b.Property(v => v.CreatedAt).HasColumnName("created_at");
        b.Property(v => v.SentVia).HasColumnName("sent_via").HasMaxLength(20);
    }
}
