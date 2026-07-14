using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class PayoutConfiguration : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("payouts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DriverId).HasColumnName("driver_id");
        builder.Property(p => p.StripePayoutId).HasColumnName("stripe_payout_id").IsRequired().HasMaxLength(255);
        builder.Property(p => p.Amount).HasColumnName("amount").HasPrecision(10, 2);
        builder.Property(p => p.Currency).HasColumnName("currency").IsRequired().HasMaxLength(3);
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.ArrivedAtUtc).HasColumnName("arrived_at_utc");

        builder.HasOne(p => p.Driver)
            .WithMany()
            .HasForeignKey(p => p.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.StripePayoutId).IsUnique();
        builder.HasIndex(p => p.DriverId);
    }
}
