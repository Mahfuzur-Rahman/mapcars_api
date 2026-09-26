using Mapcars.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mapcars.Infrastructure.Persistence.Configurations;

/// <summary>database/041. snake_case feature columns, PascalCase base columns.</summary>
public class DriverStatementConfiguration : IEntityTypeConfiguration<DriverStatement>
{
    public void Configure(EntityTypeBuilder<DriverStatement> b)
    {
        b.ToTable("driver_statements");
        b.HasKey(x => x.Id);

        b.Property(x => x.DriverId).HasColumnName("driver_id");
        b.Property(x => x.PeriodStartUtc).HasColumnName("period_start_utc");
        b.Property(x => x.PeriodEndUtc).HasColumnName("period_end_utc");
        b.Property(x => x.TripCount).HasColumnName("trip_count");
        b.Property(x => x.GrossFaresPence).HasColumnName("gross_fares_pence");
        b.Property(x => x.TipsPence).HasColumnName("tips_pence");
        b.Property(x => x.CommissionPence).HasColumnName("commission_pence");
        b.Property(x => x.DriverEarningsPence).HasColumnName("driver_earnings_pence");
        b.Property(x => x.CardPayablePence).HasColumnName("card_payable_pence");
        b.Property(x => x.CashCommissionOwedPence).HasColumnName("cash_commission_owed_pence");
        b.Property(x => x.NetPence).HasColumnName("net_pence");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);

        b.HasIndex(x => new { x.DriverId, x.PeriodStartUtc }).IsUnique();

        b.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId);
        b.HasMany(x => x.Lines).WithOne(l => l.Statement).HasForeignKey(l => l.StatementId);
    }
}

public class DriverStatementLineConfiguration : IEntityTypeConfiguration<DriverStatementLine>
{
    public void Configure(EntityTypeBuilder<DriverStatementLine> b)
    {
        b.ToTable("driver_statement_lines");
        b.HasKey(x => x.Id);

        b.Property(x => x.StatementId).HasColumnName("statement_id");
        b.Property(x => x.TripId).HasColumnName("trip_id");
        b.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        b.Property(x => x.PaymentMethod).HasColumnName("payment_method").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FarePence).HasColumnName("fare_pence");
        b.Property(x => x.TipPence).HasColumnName("tip_pence");
        b.Property(x => x.CommissionPence).HasColumnName("commission_pence");
        b.Property(x => x.DriverEarningsPence).HasColumnName("driver_earnings_pence");
        b.Property(x => x.AmountPence).HasColumnName("amount_pence");

        b.HasIndex(x => x.TripId).IsUnique();
    }
}
