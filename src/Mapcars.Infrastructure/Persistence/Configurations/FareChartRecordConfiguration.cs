using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class FareChartRecordConfiguration : IEntityTypeConfiguration<FareChartRecord>
{
    public void Configure(EntityTypeBuilder<FareChartRecord> builder)
    {
        builder.ToTable("fare_charts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Version).IsRequired();
        builder.HasIndex(x => x.Version).IsUnique();

        // Whole-document config — stored as jsonb, never queried column-wise.
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
    }
}
