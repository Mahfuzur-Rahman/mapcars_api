using Mapcars.Application.Trips.Mapping;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Tests;

/// <summary>
/// The driver is always paid, so the driver never learns how the customer's card
/// went. A regression here is silent — nothing crashes, a driver just starts
/// seeing "Failed" and asks the customer about it at the kerb.
/// </summary>
public class DriverPaymentViewTests
{
    private static readonly DateTime Dropoff = new(2026, 9, 26, 18, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(PaymentStatus.Processing)]
    [InlineData(PaymentStatus.ActionRequired)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Refunded)]
    [InlineData(PaymentStatus.Collected)]
    public void Every_post_dropoff_card_state_reads_as_collected(PaymentStatus actual)
        => Assert.Equal(PaymentStatus.Collected, DriverPaymentView.StatusFor(actual));

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Voided)]
    public void States_the_driver_may_see_pass_through(PaymentStatus actual)
        => Assert.Equal(actual, DriverPaymentView.StatusFor(actual));

    [Fact]
    public void A_hold_is_not_a_settlement()
        => Assert.Equal(PaymentStatus.Pending, DriverPaymentView.StatusFor(PaymentStatus.Authorized));

    [Fact]
    public void Every_member_maps_into_the_three_driver_states()
    {
        var allowed = new[] { PaymentStatus.Pending, PaymentStatus.Collected, PaymentStatus.Voided };
        foreach (var s in Enum.GetValues<PaymentStatus>())
            Assert.Contains(DriverPaymentView.StatusFor(s), allowed);
    }

    [Fact]
    public void A_late_card_recovery_does_not_leak_through_its_timestamp()
    {
        var trip = new Trip
        {
            PickupAddress = "A",
            DropoffAddress = "B",
            PaymentMethod = PaymentMethod.Card,
            PaymentStatus = PaymentStatus.Collected,
            CompletedAtUtc = Dropoff,
            PaidAtUtc = Dropoff.AddHours(6), // collected on a retry
        };

        Assert.Equal(Dropoff, DriverPaymentView.PaidAtFor(trip));
    }

    [Fact]
    public void A_failed_card_trip_still_reads_as_paid_at_dropoff()
    {
        var trip = new Trip
        {
            PickupAddress = "A",
            DropoffAddress = "B",
            PaymentMethod = PaymentMethod.Card,
            PaymentStatus = PaymentStatus.Failed,
            CompletedAtUtc = Dropoff,
            PaidAtUtc = null,
        };

        Assert.Equal(Dropoff, DriverPaymentView.PaidAtFor(trip));
        Assert.Equal("Collected", trip.ToResponse(forDriver: true).PaymentStatus);
    }

    [Fact]
    public void The_customer_view_is_untouched()
    {
        var trip = new Trip
        {
            PickupAddress = "A",
            DropoffAddress = "B",
            PaymentMethod = PaymentMethod.Card,
            PaymentStatus = PaymentStatus.Failed,
            CompletedAtUtc = Dropoff,
        };

        var view = trip.ToResponse();
        Assert.Equal("Failed", view.PaymentStatus);
        Assert.Null(view.PaidAtUtc);
    }

    [Fact]
    public void Cash_keeps_its_real_collection_time()
    {
        var trip = new Trip
        {
            PickupAddress = "A",
            DropoffAddress = "B",
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Collected,
            CompletedAtUtc = Dropoff,
            PaidAtUtc = Dropoff.AddSeconds(2),
        };

        Assert.Equal(Dropoff.AddSeconds(2), DriverPaymentView.PaidAtFor(trip));
    }

    [Fact]
    public void An_unsettled_trip_has_no_paid_time()
        => Assert.Null(DriverPaymentView.PaidAtFor(new Trip { PickupAddress = "A", DropoffAddress = "B", PaymentStatus = PaymentStatus.Pending }));
}
