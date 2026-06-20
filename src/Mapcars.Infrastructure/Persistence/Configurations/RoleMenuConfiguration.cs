using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class RoleMenuConfiguration : IEntityTypeConfiguration<RoleMenu>
{
    public void Configure(EntityTypeBuilder<RoleMenu> b)
    {
        b.ToTable("role_menus");
        b.HasKey(rm => new { rm.RoleId, rm.MenuId });
        b.Property(rm => rm.RoleId).HasColumnName("role_id");
        b.Property(rm => rm.MenuId).HasColumnName("menu_id");

        b.HasOne(rm => rm.Role)
            .WithMany(r => r.RoleMenus)
            .HasForeignKey(rm => rm.RoleId);

        b.HasOne(rm => rm.Menu)
            .WithMany(m => m.RoleMenus)
            .HasForeignKey(rm => rm.MenuId);
    }
}
