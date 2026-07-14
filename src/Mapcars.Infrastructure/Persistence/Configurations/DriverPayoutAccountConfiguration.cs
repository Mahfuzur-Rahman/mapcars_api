using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class DriverPayoutAccountConfiguration : IEntityTypeConfiguration<DriverPayoutAccount>
{
    public void Configure(EntityTypeBuilder<DriverPayoutAccount> builder)
    {
        builder.ToTable("driver_payout_accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.DriverId).HasColumnName("driver_id");
        builder.Property(a => a.StripeAccountId).HasColumnName("stripe_account_id").IsRequired().HasMaxLength(255);
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.PayoutsEnabled).HasColumnName("payouts_enabled");
        builder.Property(a => a.ChargesEnabled).HasColumnName("charges_enabled");

        builder.HasOne(a => a.Driver)
            .WithMany()
            .HasForeignKey(a => a.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.DriverId).IsUnique();
        builder.HasIndex(a => a.StripeAccountId).IsUnique();
    }
}
