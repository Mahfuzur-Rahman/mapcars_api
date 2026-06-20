using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> b)
    {
        b.ToTable("admins");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).HasColumnName("id");
        b.Property(a => a.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        b.Property(a => a.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        b.Property(a => a.FullName).HasColumnName("full_name").HasMaxLength(100).IsRequired();
        b.Property(a => a.RoleId).HasColumnName("role_id");
        b.Property(a => a.IsActive).HasColumnName("is_active");
        b.Property(a => a.CreatedBy).HasColumnName("created_by");
        b.Property(a => a.CreatedAtUtc).HasColumnName("created_at");
        b.Property(a => a.UpdatedAtUtc).HasColumnName("updated_at");
        b.HasIndex(a => a.Email).IsUnique();

        b.HasOne(a => a.Role)
            .WithMany(r => r.Admins)
            .HasForeignKey(a => a.RoleId);

        b.HasOne(a => a.Creator)
            .WithMany()
            .HasForeignKey(a => a.CreatedBy)
            .IsRequired(false);
    }
}
