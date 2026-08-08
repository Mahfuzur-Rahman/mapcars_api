using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class ErrorLogConfiguration : IEntityTypeConfiguration<ErrorLog>
{
    public void Configure(EntityTypeBuilder<ErrorLog> builder)
    {
        builder.ToTable("error_logs");
        builder.HasKey(e => e.Id);

        // Enums are stored as their names, not ints — an error log is read by a
        // human in a SQL client as often as through the portal, and 'DriverApp'
        // beats '3'. See database/021_error_logs.sql.
        builder.Property(e => e.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Level).HasColumnName("level").HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(e => e.Message).HasColumnName("message").IsRequired().HasMaxLength(2000);
        builder.Property(e => e.ExceptionType).HasColumnName("exception_type").HasMaxLength(200);
        builder.Property(e => e.StackTrace).HasColumnName("stack_trace");

        builder.Property(e => e.Path).HasColumnName("path").HasMaxLength(500);
        builder.Property(e => e.HttpMethod).HasColumnName("http_method").HasMaxLength(10);
        builder.Property(e => e.StatusCode).HasColumnName("status_code");

        builder.Property(e => e.UserType).HasColumnName("user_type").HasMaxLength(20);
        builder.Property(e => e.UserId).HasColumnName("user_id");

        builder.Property(e => e.AppVersion).HasColumnName("app_version").HasMaxLength(50);
        builder.Property(e => e.Platform).HasColumnName("platform").HasMaxLength(50);
        builder.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
        builder.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);

        builder.Property(e => e.IsResolved).HasColumnName("is_resolved");
        builder.Property(e => e.ResolvedAtUtc).HasColumnName("resolved_at_utc");
        builder.Property(e => e.ResolvedByAdminId).HasColumnName("resolved_by_admin_id");

        builder.HasIndex(e => e.CreatedAtUtc);
        builder.HasIndex(e => new { e.Source, e.CreatedAtUtc });
        builder.HasIndex(e => new { e.Level, e.CreatedAtUtc });
    }
}
