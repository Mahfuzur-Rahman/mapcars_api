using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class AppSettingRecordConfiguration : IEntityTypeConfiguration<AppSettingRecord>
{
    public void Configure(EntityTypeBuilder<AppSettingRecord> builder)
    {
        builder.ToTable("app_settings");
        builder.HasKey(x => x.Id);

        // snake_case feature columns, PascalCase base columns — see database/033_app_settings.sql.
        builder.Property(x => x.Key).HasColumnName("key").IsRequired().HasMaxLength(100);
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.UpdatedByAdminId).HasColumnName("updated_by_admin_id");

        // Whole-document config — stored as jsonb, never queried column-wise.
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").IsRequired().HasColumnType("jsonb");

        // One row per (key, version). Unique in the database as well as here,
        // because it is what stops two API instances minting the same version.
        builder.HasIndex(x => new { x.Key, x.Version }).IsUnique();
    }
}
