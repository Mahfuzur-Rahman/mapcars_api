using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        b.Property(r => r.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        b.Property(r => r.Description).HasColumnName("description");
        b.Property(r => r.CreatedAtUtc).HasColumnName("created_at");
        b.HasIndex(r => r.Name).IsUnique();
    }
}
