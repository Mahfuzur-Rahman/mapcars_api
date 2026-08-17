using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(d => d.Id);

        // Column casing note: base columns are PascalCase (EF default), the
        // descriptive columns added here follow the snake_case convention used
        // by riders/drivers' auth columns — see database/005_documents.sql.
        builder.Property(d => d.RiderId).HasColumnName("rider_id");
        builder.Property(d => d.DriverId).HasColumnName("driver_id");
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(d => d.StorageKey).HasColumnName("storage_key").IsRequired().HasMaxLength(260);
        builder.Property(d => d.OriginalFileName).HasColumnName("original_file_name").IsRequired().HasMaxLength(260);
        builder.Property(d => d.ContentType).HasColumnName("content_type").IsRequired().HasMaxLength(100);
        builder.Property(d => d.ReviewStatus).HasColumnName("review_status").HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.ReviewedAtUtc).HasColumnName("reviewed_at_utc");
        builder.Property(d => d.ExpiresOn).HasColumnName("expires_on");
        builder.Property(d => d.IsDeletionRequested).HasColumnName("is_deletion_requested");
        builder.Property(d => d.DeletionReason).HasColumnName("deletion_reason");
        builder.Property(d => d.DeletionRequestedAtUtc).HasColumnName("deletion_requested_at_utc");

        builder.HasOne(d => d.Rider)
            .WithMany()
            .HasForeignKey(d => d.RiderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Driver)
            .WithMany()
            .HasForeignKey(d => d.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.RiderId);
        builder.HasIndex(d => d.DriverId);
        builder.HasIndex(d => d.IsDeletionRequested);
    }
}
