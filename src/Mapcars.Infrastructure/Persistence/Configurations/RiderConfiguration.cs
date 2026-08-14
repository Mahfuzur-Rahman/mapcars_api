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

        b.Property(r => r.EmergencyContactName).HasColumnName("emergency_contact_name").HasMaxLength(200);
        b.Property(r => r.EmergencyContactPhone).HasColumnName("emergency_contact_phone").HasMaxLength(20);
        b.Property(r => r.MarketingConsent).HasColumnName("marketing_consent");
        b.Property(r => r.AccessibilityNeeds).HasColumnName("accessibility_needs").HasMaxLength(500);
        b.Property(r => r.ProfilePictureKey).HasMaxLength(255);
        b.Property(r => r.ProfilePictureContentType).HasMaxLength(100);

        b.Property(r => r.CancellationCount).HasColumnName("cancellation_count");
        b.Property(r => r.NoShowCount).HasColumnName("no_show_count");
        b.Property(r => r.AverageRating).HasColumnName("average_rating").HasPrecision(3, 2);
        b.Property(r => r.RatingCount).HasColumnName("rating_count");

        b.HasIndex(r => r.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
        b.Ignore(r => r.IsProfileComplete);
    }
}
