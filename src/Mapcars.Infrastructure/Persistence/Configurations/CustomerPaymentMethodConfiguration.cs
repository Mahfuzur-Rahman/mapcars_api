using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

public class CustomerPaymentMethodConfiguration : IEntityTypeConfiguration<CustomerPaymentMethod>
{
    public void Configure(EntityTypeBuilder<CustomerPaymentMethod> b)
    {
        b.ToTable("customer_payment_methods");
        b.HasKey(x => x.Id);

        // snake_case feature columns, PascalCase base columns — the convention the
        // payments family already uses (006/007). See database/037.
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.StripePaymentMethodId)
            .HasColumnName("stripe_payment_method_id").IsRequired().HasMaxLength(255);
        b.Property(x => x.Type).HasColumnName("type").IsRequired().HasMaxLength(30);
        b.Property(x => x.Brand).HasColumnName("brand").HasMaxLength(30);
        b.Property(x => x.Last4).HasColumnName("last4").HasMaxLength(4);
        b.Property(x => x.ExpMonth).HasColumnName("exp_month");
        b.Property(x => x.ExpYear).HasColumnName("exp_year");
        b.Property(x => x.FundingType).HasColumnName("funding_type").HasMaxLength(20);
        b.Property(x => x.Country).HasColumnName("country").HasMaxLength(2);
        b.Property(x => x.IsDefault).HasColumnName("is_default");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.DeactivatedAtUtc).HasColumnName("deactivated_at_utc");
        b.Property(x => x.DeactivationReason).HasColumnName("deactivation_reason").HasMaxLength(100);
        b.Property(x => x.StripeSetupIntentId).HasColumnName("stripe_setup_intent_id").HasMaxLength(255);
        b.Property(x => x.MandateAcceptedAtUtc).HasColumnName("mandate_accepted_at_utc");

        b.HasIndex(x => x.StripePaymentMethodId).IsUnique();
        b.HasIndex(x => new { x.CustomerId, x.IsActive });

        b.HasOne(x => x.Customer)
            .WithMany(c => c.PaymentMethods)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // IsUsable / IsExpired are computed from the columns above.
        b.Ignore(x => x.IsUsable);
    }
}
