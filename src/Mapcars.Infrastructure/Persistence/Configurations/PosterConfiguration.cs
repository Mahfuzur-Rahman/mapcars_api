using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class PosterConfiguration : IEntityTypeConfiguration<Poster>
{
    public void Configure(EntityTypeBuilder<Poster> builder)
    {
        builder.ToTable("posters");
        builder.HasKey(p => p.Id);

        // Column casing note: base columns are PascalCase (EF default), the
        // descriptive columns added here follow the snake_case convention used
        // by documents/riders — see database/020_posters.sql.
        builder.Property(p => p.StorageKey).HasColumnName("storage_key").IsRequired().HasMaxLength(260);
        builder.Property(p => p.ContentType).HasColumnName("content_type").IsRequired().HasMaxLength(100);
        builder.Property(p => p.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(p => p.Subtitle).HasColumnName("subtitle").HasMaxLength(300);
        builder.Property(p => p.LinkUrl).HasColumnName("link_url").HasMaxLength(2048);
        builder.Property(p => p.SortOrder).HasColumnName("sort_order");
        builder.Property(p => p.IsActive).HasColumnName("is_active");
        builder.Property(p => p.CreatedByAdminId).HasColumnName("created_by_admin_id");

        builder.HasIndex(p => new { p.IsActive, p.SortOrder });
    }
}
