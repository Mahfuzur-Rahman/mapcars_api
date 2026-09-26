using Mapcars.Application.Payments;
using Mapcars.Application.Settings.Models;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Tests;

/// <summary>
/// Booking is where the admin payment switches become true or false. A gap
/// here is a fare nobody will ever charge.
/// </summary>
public class CustomerPaymentEligibilityTests
{
    private static PaymentSettings Settings(bool cash = true, bool card = true, int debtLimit = 0)
        => new() { CashEnabled = cash, CardEnabled = card, BlockBookingWhenDebtExceedsPence = debtLimit };

    [Fact]
    public void Defaults_allow_cash_and_refuse_card()
    {
        var defaults = new PaymentSettings();
        Assert.True(CustomerPaymentEligibility.Check(defaults, PaymentMethod.Cash, false, 0).Allowed);
        Assert.False(CustomerPaymentEligibility.Check(defaults, PaymentMethod.Card, true, 0).Allowed);
    }

    [Fact]
    public void Card_is_refused_while_switched_off_even_with_a_saved_card()
        => Assert.False(CustomerPaymentEligibility.Check(Settings(card: false), PaymentMethod.Card, true, 0).Allowed);

    [Fact]
    public void Card_needs_a_usable_saved_card()
    {
        var result = CustomerPaymentEligibility.Check(Settings(), PaymentMethod.Card, false, 0);
        Assert.False(result.Allowed);
        Assert.Contains("Add a card", result.Reason);
    }

    [Fact]
    public void Card_with_a_saved_card_is_allowed()
        => Assert.True(CustomerPaymentEligibility.Check(Settings(), PaymentMethod.Card, true, 0).Allowed);

    [Fact]
    public void Cash_is_refused_once_switched_off()
        => Assert.False(CustomerPaymentEligibility.Check(Settings(cash: false), PaymentMethod.Cash, false, 0).Allowed);

    [Fact]
    public void Any_debt_blocks_when_the_limit_is_zero()
    {
        var result = CustomerPaymentEligibility.Check(Settings(), PaymentMethod.Cash, false, 1);
        Assert.False(result.Allowed);
        Assert.Contains("£0.01", result.Reason);
    }

    [Fact]
    public void Debt_within_the_limit_is_tolerated()
        => Assert.True(CustomerPaymentEligibility.Check(Settings(debtLimit: 1000), PaymentMethod.Cash, false, 1000).Allowed);

    [Fact]
    public void Debt_over_the_limit_blocks_cash_too()
        => Assert.False(CustomerPaymentEligibility.Check(Settings(debtLimit: 1000), PaymentMethod.Cash, false, 1001).Allowed);
}
