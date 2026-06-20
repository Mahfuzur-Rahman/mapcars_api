using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> b)
    {
        b.ToTable("drivers");
        b.HasKey(d => d.Id);

        b.Property(d => d.FullName).HasMaxLength(200);
        b.Property(d => d.Email).HasMaxLength(256);
        b.Property(d => d.PhoneNumber).HasMaxLength(20);
        b.Property(d => d.PhvLicenceNumber).HasMaxLength(50);
        b.Property(d => d.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
        b.Property(d => d.GoogleSub).HasColumnName("google_sub").HasMaxLength(255);
        b.Property(d => d.IsEmailVerified).HasColumnName("is_email_verified");
        b.Property(d => d.IsPhoneVerified).HasColumnName("is_phone_verified");
        b.Property(d => d.Status).HasConversion<string>().HasMaxLength(30);

        b.HasIndex(d => d.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
        b.HasIndex(d => d.PhvLicenceNumber).IsUnique().HasFilter("\"PhvLicenceNumber\" IS NOT NULL");
        b.Ignore(d => d.IsProfileComplete);
    }
}
