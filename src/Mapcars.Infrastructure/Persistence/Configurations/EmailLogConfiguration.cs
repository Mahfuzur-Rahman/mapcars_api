using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("email_log");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ToEmail).HasColumnName("to_email").IsRequired().HasMaxLength(320);
        builder.Property(e => e.FromAddress).HasColumnName("from_address").IsRequired().HasMaxLength(320);
        builder.Property(e => e.FromName).HasColumnName("from_name").HasMaxLength(200);

        builder.Property(e => e.Subject).HasColumnName("subject").IsRequired().HasMaxLength(500);
        builder.Property(e => e.BodyHtml).HasColumnName("body_html").IsRequired();

        builder.Property(e => e.Provider).HasColumnName("provider").IsRequired().HasMaxLength(20);
        builder.Property(e => e.Category).HasColumnName("category").IsRequired().HasMaxLength(50);
        builder.Property(e => e.Status).HasColumnName("status").IsRequired().HasMaxLength(20);
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);

        builder.Property(e => e.SentByAdminId).HasColumnName("sent_by_admin_id");

        builder.HasIndex(e => e.CreatedAtUtc);
        builder.HasIndex(e => new { e.Category, e.CreatedAtUtc });
        builder.HasIndex(e => new { e.Status, e.CreatedAtUtc });
    }
}
