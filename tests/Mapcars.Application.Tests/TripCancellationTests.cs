using Mapcars.Application.Trips;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Tests;

/// <summary>
/// Who may cancel a trip, and from which status.
///
/// Getting this wrong costs money rather than crashing: cancelling an
/// in-progress trip voids a fare the customer is already riding, and cancelling
/// an expired one turns a platform failure into a strike on the customer.
/// </summary>
public class TripCancellationTests
{
    // Every status, with the expected answer for each party. Keyed by status so a
    // new TripStatus member fails the theory below until someone decides its rule.
    private static readonly Dictionary<TripStatus, (bool Customer, bool Driver)> Expected = new()
    {
        [TripStatus.Requested]           = (Customer: true,  Driver: false),
        [TripStatus.DriverAssigned]      = (Customer: true,  Driver: true),
        [TripStatus.DriverArrived]       = (Customer: true,  Driver: true),
        [TripStatus.InProgress]          = (Customer: false, Driver: false),
        [TripStatus.Completed]           = (Customer: false, Driver: false),
        [TripStatus.CancelledByCustomer] = (Customer: false, Driver: false),
        [TripStatus.CancelledByDriver]   = (Customer: false, Driver: false),
        [TripStatus.Expired]             = (Customer: false, Driver: false),
    };

    public static TheoryData<TripStatus> AllStatuses()
    {
        var data = new TheoryData<TripStatus>();
        foreach (var status in Enum.GetValues<TripStatus>()) data.Add(status);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void A_customer_may_cancel_only_before_the_ride_starts(TripStatus status)
        => Assert.Equal(Expected[status].Customer, TripCancellation.CanCancel(status, byDriver: false));

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void A_driver_may_cancel_only_between_accepting_and_starting(TripStatus status)
        => Assert.Equal(Expected[status].Driver, TripCancellation.CanCancel(status, byDriver: true));

    [Fact]
    public void A_driver_cannot_cancel_an_open_request_they_were_never_assigned()
    {
        // A Requested trip has no driver yet, so there is nothing for a driver to
        // walk away from — declining is simply not accepting.
        Assert.False(TripCancellation.CanCancel(TripStatus.Requested, byDriver: true));
    }

    [Fact]
    public void Nobody_can_cancel_a_ride_that_is_under_way()
    {
        // The passenger is in the car. Cancelling here voided the fare and meant
        // the commission was never owed.
        Assert.False(TripCancellation.CanCancel(TripStatus.InProgress, byDriver: false));
        Assert.False(TripCancellation.CanCancel(TripStatus.InProgress, byDriver: true));
    }

    [Fact]
    public void An_expired_request_cannot_be_cancelled_into_a_strike_on_the_customer()
    {
        // The platform failed to find a driver; it must not become a
        // CancelledByCustomer that counts against them.
        Assert.False(TripCancellation.CanCancel(TripStatus.Expired, byDriver: false));
    }
}
