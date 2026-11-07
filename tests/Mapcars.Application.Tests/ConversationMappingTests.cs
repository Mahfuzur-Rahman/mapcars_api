using Mapcars.Application.Messages.Interfaces;
using Mapcars.Application.Messages.Mapping;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Tests;

/// <summary>What each party sees for one row of their Messages list.</summary>
public class ConversationMappingTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    private static ConversationRow Row(
        TripStatus status = TripStatus.InProgress, DateTime? completed = null,
        string? customer = "Sara Khan", string? driver = "Ahmed Ali",
        string? lastContent = "I'm outside", string? lastSender = "rider", int unread = 2)
        => new(Guid.Parse("11111111-1111-1111-1111-111111111111"), status,
            Now.AddHours(-1), completed, "Heathrow T5, Hounslow", "Soho Square, London",
            customer, driver, lastContent, lastSender, lastContent is null ? null : Now.AddMinutes(-5), unread);

    [Fact]
    public void The_customer_sees_the_driver_and_the_driver_sees_the_customer()
    {
        Assert.Equal("Ahmed Ali", Row().ToResponse("customer", Now, Window).CounterpartName);
        Assert.Equal("Sara Khan", Row().ToResponse("driver", Now, Window).CounterpartName);
    }

    [Fact]
    public void A_missing_name_falls_back_to_the_role()
    {
        Assert.Equal("Driver", Row(driver: null).ToResponse("customer", Now, Window).CounterpartName);
        Assert.Equal("Passenger", Row(customer: " ").ToResponse("driver", Now, Window).CounterpartName);
    }

    [Fact]
    public void The_last_sender_is_reported_in_the_current_spelling()
        => Assert.Equal("customer", Row().ToResponse("driver", Now, Window).LastMessage!.SenderType);

    [Fact]
    public void A_trip_with_no_messages_has_no_preview()
        => Assert.Null(Row(lastContent: null, unread: 0).ToResponse("customer", Now, Window).LastMessage);

    [Fact]
    public void Can_send_follows_the_chat_policy()
    {
        Assert.True(Row().ToResponse("customer", Now, Window).CanSend);
        Assert.False(Row(TripStatus.Completed, Now.AddDays(-2)).ToResponse("customer", Now, Window).CanSend);
    }

    [Fact]
    public void Status_and_unread_pass_through()
    {
        var r = Row().ToResponse("customer", Now, Window);
        Assert.Equal("InProgress", r.TripStatus);
        Assert.Equal(2, r.UnreadCount);
    }
}
