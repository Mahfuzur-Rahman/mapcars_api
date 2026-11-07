using Mapcars.Application.Messages;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Tests;

/// <summary>
/// When a trip's chat accepts new messages, and whose messages count as unread.
/// Chat stays readable forever; these rules only decide sending and badges.
/// </summary>
public class ChatPolicyTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    [Theory]
    [InlineData(TripStatus.DriverAssigned)]
    [InlineData(TripStatus.DriverArrived)]
    [InlineData(TripStatus.InProgress)]
    public void A_live_trip_is_always_open(TripStatus status)
        => Assert.True(ChatPolicy.CanSend(status, completedAtUtc: null, Now, Window));

    [Fact]
    public void A_request_with_no_driver_has_nobody_to_talk_to()
        => Assert.False(ChatPolicy.CanSend(TripStatus.Requested, null, Now, Window));

    [Fact]
    public void A_completed_trip_stays_open_for_lost_items_within_the_window()
        => Assert.True(ChatPolicy.CanSend(TripStatus.Completed, Now.AddHours(-23), Now, Window));

    [Fact]
    public void A_completed_trip_closes_once_the_window_has_passed()
        => Assert.False(ChatPolicy.CanSend(TripStatus.Completed, Now.AddHours(-25), Now, Window));

    [Fact]
    public void A_completed_trip_with_no_completion_time_is_closed()
        => Assert.False(ChatPolicy.CanSend(TripStatus.Completed, null, Now, Window));

    [Theory]
    [InlineData(TripStatus.CancelledByCustomer)]
    [InlineData(TripStatus.CancelledByDriver)]
    [InlineData(TripStatus.Expired)]
    public void Cancelled_and_expired_trips_are_closed_immediately(TripStatus status)
        => Assert.False(ChatPolicy.CanSend(status, null, Now, Window));

    [Theory]
    [InlineData("customer", "driver", true)]
    [InlineData("customer", "customer", false)]
    [InlineData("customer", "rider", false)]   // legacy spelling of the customer
    [InlineData("driver", "customer", true)]
    [InlineData("driver", "rider", true)]
    [InlineData("driver", "driver", false)]
    public void Only_the_other_partys_messages_can_be_unread(string viewer, string sender, bool expected)
        => Assert.Equal(expected, ChatPolicy.IsFromCounterpart(viewer, sender));

    [Fact]
    public void Options_default_to_a_24_hour_window()
        => Assert.Equal(TimeSpan.FromHours(24), new ChatOptions().SendWindow);
}
