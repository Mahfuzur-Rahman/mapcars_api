using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class AdminMenuPermissionConfiguration : IEntityTypeConfiguration<AdminMenuPermission>
{
    public void Configure(EntityTypeBuilder<AdminMenuPermission> b)
    {
        b.ToTable("admin_menu_permissions");
        b.HasKey(amp => new { amp.AdminId, amp.MenuId });
        b.Property(amp => amp.AdminId).HasColumnName("admin_id");
        b.Property(amp => amp.MenuId).HasColumnName("menu_id");
        b.Property(amp => amp.IsAllowed).HasColumnName("is_allowed");

        b.HasOne(amp => amp.Admin)
            .WithMany(a => a.MenuPermissions)
            .HasForeignKey(amp => amp.AdminId);

        b.HasOne(amp => amp.Menu)
            .WithMany(m => m.AdminPermissions)
            .HasForeignKey(amp => amp.MenuId);
    }
}
