using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> b)
    {
        b.ToTable("menus");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(m => m.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        b.Property(m => m.Path).HasColumnName("path").HasMaxLength(255);
        b.Property(m => m.Icon).HasColumnName("icon").HasMaxLength(50);
        b.Property(m => m.ParentId).HasColumnName("parent_id");
        b.Property(m => m.SortOrder).HasColumnName("sort_order");
        b.Property(m => m.IsActive).HasColumnName("is_active");

        b.HasOne(m => m.Parent)
            .WithMany(m => m.Children)
            .HasForeignKey(m => m.ParentId)
            .IsRequired(false);
    }
}
