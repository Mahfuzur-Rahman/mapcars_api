using Mapcars.Application.Settings;
using Mapcars.Application.Settings.Models;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Tests;

/// <summary>
/// Which driver may be offered which fare, and whether a saved card is usable.
/// Both are pure rules, and both decide whether money can be taken at all.
/// </summary>
public class DriverPaymentOptionsTests
{
    private static PaymentSettings Global(bool cash, bool card) =>
        new() { CashEnabled = cash, CardEnabled = card };

    private static Driver DriverWith(bool? cash, bool? card) =>
        new() { AcceptsCashOverride = cash, AcceptsCardOverride = card };

    [Fact]
    public void A_null_override_follows_the_global_setting()
    {
        var d = DriverWith(null, null);
        Assert.True(DriverPaymentOptions.AcceptsCash(Global(cash: true, card: false), d));
        Assert.False(DriverPaymentOptions.AcceptsCard(Global(cash: true, card: false), d));
        Assert.True(DriverPaymentOptions.AcceptsCard(Global(cash: true, card: true), d));
    }

    [Fact]
    public void An_override_can_narrow_the_global_setting()
    {
        var cardOnly = DriverWith(cash: false, card: null);
        Assert.False(DriverPaymentOptions.AcceptsCash(Global(cash: true, card: true), cardOnly));
        Assert.True(DriverPaymentOptions.AcceptsCard(Global(cash: true, card: true), cardOnly));
    }

    [Fact]
    public void An_override_can_NEVER_widen_the_global_setting()
    {
        // The rule that matters. Once cash is switched off platform-wide, no
        // per-driver flag brings it back — otherwise "cash is off" would not be
        // true, and that guarantee is the entire point of the switch.
        var wantsCash = DriverWith(cash: true, card: true);
        Assert.False(DriverPaymentOptions.AcceptsCash(Global(cash: false, card: true), wantsCash));
        Assert.False(DriverPaymentOptions.AcceptsCard(Global(cash: true, card: false), wantsCash));
    }

    [Fact]
    public void Accepts_dispatches_on_the_trips_payment_method()
    {
        var g = Global(cash: true, card: true);
        var cardOnly = DriverWith(cash: false, card: null);

        Assert.False(DriverPaymentOptions.Accepts(g, cardOnly, PaymentMethod.Cash));
        Assert.True(DriverPaymentOptions.Accepts(g, cardOnly, PaymentMethod.Card));
    }

    [Fact]
    public void Every_combination_resolves_without_surprise()
    {
        foreach (var gCash in new[] { true, false })
        foreach (var gCard in new[] { true, false })
        foreach (var oCash in new bool?[] { null, true, false })
        foreach (var oCard in new bool?[] { null, true, false })
        {
            var g = Global(gCash, gCard);
            var d = DriverWith(oCash, oCard);

            // The invariant: the effective answer is never more permissive than
            // the global setting, whatever the override says.
            if (!gCash) Assert.False(DriverPaymentOptions.AcceptsCash(g, d));
            if (!gCard) Assert.False(DriverPaymentOptions.AcceptsCard(g, d));
        }
    }

    [Fact]
    public void The_blocked_message_names_the_method_that_was_refused()
    {
        Assert.Contains("cash", DriverPaymentOptions.BlockedMessage(PaymentMethod.Cash), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("card", DriverPaymentOptions.BlockedMessage(PaymentMethod.Card), StringComparison.OrdinalIgnoreCase);
    }
}

public class CustomerPaymentMethodTests
{
    private static CustomerPaymentMethod Card(int? month, int? year, bool active = true) => new()
    {
        StripePaymentMethodId = "pm_test",
        ExpMonth = month,
        ExpYear = year,
        IsActive = active,
    };

    [Fact]
    public void A_card_is_valid_through_the_END_of_its_expiry_month()
    {
        var card = Card(9, 2026);
        Assert.False(card.IsExpired(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.False(card.IsExpired(new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc)));
        Assert.True(card.IsExpired(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void December_rolls_into_the_next_year_correctly()
    {
        var card = Card(12, 2026);
        Assert.False(card.IsExpired(new DateTime(2026, 12, 31, 23, 59, 0, DateTimeKind.Utc)));
        Assert.True(card.IsExpired(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Unknown_or_nonsense_expiry_counts_as_NOT_expired()
    {
        // The provider is the authority. Hiding a card that would have worked is
        // worse than letting it decline and saying so.
        Assert.False(Card(null, null).IsExpired(DateTime.UtcNow));
        Assert.False(Card(9, null).IsExpired(DateTime.UtcNow));
        Assert.False(Card(13, 2026).IsExpired(DateTime.UtcNow));
        Assert.False(Card(0, 2026).IsExpired(DateTime.UtcNow));
    }

    [Fact]
    public void Usable_means_active_and_not_expired()
    {
        Assert.True(Card(12, 2099).IsUsable);
        Assert.False(Card(12, 2099, active: false).IsUsable);
        Assert.False(Card(1, 2000).IsUsable);
    }
}
