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

        b.Property(d => d.FirstName).HasMaxLength(100);
        b.Property(d => d.LastName).HasMaxLength(100);
        b.Property(d => d.Address).HasMaxLength(500);
        b.Property(d => d.NationalIdNumber).HasMaxLength(50);
        b.Property(d => d.ProfilePictureKey).HasMaxLength(255);
        b.Property(d => d.ProfilePictureContentType).HasMaxLength(100);

        b.Property(d => d.DrivingLicenceNumber).HasColumnName("driving_licence_number").HasMaxLength(20);
        b.Property(d => d.PassportNumber).HasColumnName("passport_number").HasMaxLength(50);
        b.Property(d => d.EmergencyContactName).HasColumnName("emergency_contact_name").HasMaxLength(200);
        b.Property(d => d.EmergencyContactPhone).HasColumnName("emergency_contact_phone").HasMaxLength(20);
        b.Property(d => d.MarketingConsent).HasColumnName("marketing_consent");

        b.Property(d => d.CancellationCount).HasColumnName("cancellation_count");
        b.Property(d => d.NoShowCount).HasColumnName("no_show_count");
        b.Property(d => d.IsOnline).HasColumnName("is_online");
        b.Property(d => d.LastOnlineAtUtc).HasColumnName("last_online_at_utc");
        b.Property(d => d.AverageRating).HasColumnName("average_rating").HasPrecision(3, 2);
        b.Property(d => d.RatingCount).HasColumnName("rating_count");

        b.HasIndex(d => d.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
        b.HasIndex(d => d.PhvLicenceNumber).IsUnique().HasFilter("\"PhvLicenceNumber\" IS NOT NULL");
        b.HasIndex(d => d.NationalIdNumber).IsUnique().HasFilter("\"NationalIdNumber\" IS NOT NULL");
        b.HasIndex(d => d.PassportNumber).IsUnique().HasFilter("passport_number IS NOT NULL");
        b.Ignore(d => d.IsProfileComplete);
    }
}
