using Mapcars.Domain.Constants;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Messages;

/// <summary>
/// The two chat rules, kept pure so they are tested in one place and mirrored
/// exactly by <c>MessageRepository</c>'s SQL.
/// </summary>
public static class ChatPolicy
{
    /// <summary>
    /// Whether a new message may be sent. Live trips: yes. Completed: only within
    /// <paramref name="window"/> of completion (lost property). Everything else —
    /// an unassigned request, a cancellation, an expiry — is closed. History stays
    /// readable regardless; this only gates sending.
    /// </summary>
    public static bool CanSend(TripStatus status, DateTime? completedAtUtc, DateTime nowUtc, TimeSpan window)
        => status switch
        {
            TripStatus.DriverAssigned or TripStatus.DriverArrived or TripStatus.InProgress => true,
            TripStatus.Completed => completedAtUtc is { } done && nowUtc - done <= window,
            _ => false,
        };

    /// <summary>
    /// True when <paramref name="senderType"/> is the other party from the
    /// viewer's side — the only messages that can be unread. Legacy "rider"
    /// rows count as the customer.
    /// </summary>
    public static bool IsFromCounterpart(string viewerType, string senderType)
        => UserTypes.IsDriver(viewerType) ? !UserTypes.IsDriver(senderType) : UserTypes.IsDriver(senderType);
}
