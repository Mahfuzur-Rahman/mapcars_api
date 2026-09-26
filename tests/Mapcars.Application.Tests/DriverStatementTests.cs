using Mapcars.Application.Statements;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Tests;

/// <summary>
/// Weekly statements are what a driver is paid from. The expensive bugs are a
/// trip on the wrong week (UK clock changes) and a sign error (paying a driver
/// the commission they owe us).
/// </summary>
public class DriverStatementTests
{
    private static DateTime Utc(int y, int mo, int d, int h = 0, int mi = 0)
        => new(y, mo, d, h, mi, 0, DateTimeKind.Utc);

    // ── The week ──────────────────────────────────────────────────────────────

    [Fact]
    public void A_winter_week_starts_at_monday_midnight_utc()
    {
        // 2026-01-14 is a Wednesday; GMT = UTC.
        var p = StatementPeriod.Containing(Utc(2026, 1, 14, 12));
        Assert.Equal(Utc(2026, 1, 12), p.StartUtc);
        Assert.Equal(Utc(2026, 1, 19), p.EndUtc);
    }

    [Fact]
    public void A_summer_week_starts_at_sunday_2300_utc()
    {
        // 2026-07-15 is a Wednesday; BST = UTC+1, so Monday 00:00 local is Sunday 23:00 UTC.
        var p = StatementPeriod.Containing(Utc(2026, 7, 15, 12));
        Assert.Equal(Utc(2026, 7, 12, 23), p.StartUtc);
        Assert.Equal(Utc(2026, 7, 19, 23), p.EndUtc);
    }

    [Fact]
    public void A_sunday_night_summer_shift_lands_on_the_week_it_was_driven()
    {
        // Sunday 23:30 UTC in summer is already Monday 00:30 in London.
        var p = StatementPeriod.Containing(Utc(2026, 7, 19, 23, 30));
        Assert.Equal(Utc(2026, 7, 19, 23), p.StartUtc);
    }

    [Fact]
    public void The_clocks_go_forward_week_is_167_hours()
    {
        // Clocks go forward Sunday 2026-03-29.
        var p = StatementPeriod.Containing(Utc(2026, 3, 25, 12));
        Assert.Equal(Utc(2026, 3, 23), p.StartUtc);
        Assert.Equal(Utc(2026, 3, 29, 23), p.EndUtc);
        Assert.Equal(TimeSpan.FromHours(167), p.EndUtc - p.StartUtc);
    }

    [Fact]
    public void The_clocks_go_back_week_is_169_hours()
    {
        // Clocks go back Sunday 2026-10-25.
        var p = StatementPeriod.Containing(Utc(2026, 10, 21, 12));
        Assert.Equal(TimeSpan.FromHours(169), p.EndUtc - p.StartUtc);
    }

    [Fact]
    public void Consecutive_weeks_tile_with_no_gap_or_overlap()
    {
        var p = StatementPeriod.Containing(Utc(2026, 11, 30));
        for (var i = 0; i < 60; i++)
        {
            var prev = p.Previous();
            Assert.Equal(prev.EndUtc, p.StartUtc);
            p = prev;
        }
    }

    [Fact]
    public void A_week_is_not_built_until_its_grace_has_passed()
    {
        // Monday 2026-01-19 00:30 UTC (GMT): last week closed 30 minutes ago —
        // inside the 1h grace — so the latest buildable week is the one before.
        var latest = StatementPeriod.RecentlyClosed(Utc(2026, 1, 19, 0, 30), 1)[0];
        Assert.Equal(Utc(2026, 1, 5), latest.StartUtc);

        var after = StatementPeriod.RecentlyClosed(Utc(2026, 1, 19, 1, 30), 1)[0];
        Assert.Equal(Utc(2026, 1, 12), after.StartUtc);
    }

    [Fact]
    public void Recently_closed_is_newest_first_and_contiguous()
    {
        var weeks = StatementPeriod.RecentlyClosed(Utc(2026, 1, 21), 4);
        Assert.Equal(4, weeks.Count);
        for (var i = 1; i < weeks.Count; i++) Assert.Equal(weeks[i].EndUtc, weeks[i - 1].StartUtc);
    }

    // ── The money ─────────────────────────────────────────────────────────────

    private static Trip TripOf(PaymentMethod method, decimal fare, decimal fee, decimal tip = 0m,
        PaymentStatus status = PaymentStatus.Collected) => new()
    {
        PickupAddress = "A",
        DropoffAddress = "B",
        DriverId = Guid.NewGuid(),
        Status = TripStatus.Completed,
        CompletedAtUtc = Utc(2026, 1, 14, 12),
        PaymentMethod = method,
        PaymentStatus = status,
        FareAmount = fare,
        PlatformFeeAmount = fee,
        DriverEarnings = fare - fee,
        TipAmount = tip,
    };

    [Fact]
    public void A_card_trip_owes_the_driver_their_share_and_the_tip()
    {
        var line = StatementBuilder.LineFor(TripOf(PaymentMethod.Card, 20.00m, 3.00m, tip: 2.50m));
        Assert.Equal(1700 + 250, line.AmountPence);
    }

    [Fact]
    public void A_cash_trip_means_the_driver_owes_the_commission()
    {
        var line = StatementBuilder.LineFor(TripOf(PaymentMethod.Cash, 20.00m, 3.00m, tip: 2.50m));
        Assert.Equal(-300, line.AmountPence);
    }

    [Fact]
    public void A_failed_card_charge_is_still_owed_to_the_driver()
    {
        // The platform absorbs failed charges — the driver is always paid.
        var line = StatementBuilder.LineFor(TripOf(PaymentMethod.Card, 20.00m, 3.00m, status: PaymentStatus.Failed));
        Assert.Equal(1700, line.AmountPence);
    }

    [Fact]
    public void The_statement_nets_card_payable_against_cash_commission()
    {
        var driver = Guid.NewGuid();
        var period = StatementPeriod.Containing(Utc(2026, 1, 14));
        var s = StatementBuilder.Build(driver, period,
        [
            TripOf(PaymentMethod.Card, 20.00m, 3.00m, tip: 1.00m), // +18.00
            TripOf(PaymentMethod.Cash, 40.00m, 6.00m),             // -6.00
            TripOf(PaymentMethod.Cash, 10.00m, 1.50m),             // -1.50
        ]);

        Assert.Equal(3, s.TripCount);
        Assert.Equal(7000, s.GrossFaresPence);
        Assert.Equal(100, s.TipsPence);
        Assert.Equal(1050, s.CommissionPence);
        Assert.Equal(5950, s.DriverEarningsPence);
        Assert.Equal(1800, s.CardPayablePence);
        Assert.Equal(750, s.CashCommissionOwedPence);
        Assert.Equal(1800 - 750, s.NetPence);
        Assert.Equal(s.Lines.Sum(l => l.AmountPence), s.NetPence);
        Assert.All(s.Lines, l => Assert.Equal(s.Id, l.StatementId));
    }

    [Fact]
    public void An_all_cash_week_nets_negative()
    {
        var s = StatementBuilder.Build(Guid.NewGuid(), StatementPeriod.Containing(Utc(2026, 1, 14)),
            [TripOf(PaymentMethod.Cash, 30.00m, 4.50m)]);
        Assert.Equal(-450, s.NetPence);
        Assert.Equal(0, s.CardPayablePence);
    }
}
