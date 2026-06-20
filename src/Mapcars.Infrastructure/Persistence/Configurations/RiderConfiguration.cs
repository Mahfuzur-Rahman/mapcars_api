using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class RiderConfiguration : IEntityTypeConfiguration<Rider>
{
    public void Configure(EntityTypeBuilder<Rider> b)
    {
        b.ToTable("riders");
        b.HasKey(r => r.Id);

        b.Property(r => r.FullName).HasMaxLength(200);
        b.Property(r => r.Email).HasMaxLength(256);
        b.Property(r => r.PhoneNumber).HasMaxLength(20);
        b.Property(r => r.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
        b.Property(r => r.GoogleSub).HasColumnName("google_sub").HasMaxLength(255);
        b.Property(r => r.IsEmailVerified).HasColumnName("is_email_verified");
        b.Property(r => r.IsPhoneVerified).HasColumnName("is_phone_verified");

        b.HasIndex(r => r.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
        b.Ignore(r => r.IsProfileComplete);
    }
}
