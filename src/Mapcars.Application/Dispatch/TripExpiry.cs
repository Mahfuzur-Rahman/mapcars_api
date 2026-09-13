using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;

namespace Mapcars.Application.Dispatch;

/// <summary>
/// How long an open request stays live, and how long the rider gets to keep it
/// alive once it lapses.
///
/// A request used to have no deadline at all: <see cref="DispatchRadius"/>
/// widened the ring to its maximum at two minutes and then stopped, leaving the
/// trip <see cref="TripStatus.Requested"/> until the rider gave up by hand. On a
/// quiet night that is a rider watching a spinner forever, and a stale card on a
/// driver's board for as long as the process lives.
///
/// The window is deliberately a minute longer than the radius escalation, so
/// every request reaches its widest audience before it can lapse.
///
/// **This is the single source of truth for those timings**, the way
/// <see cref="DispatchRadius"/> is for reach. Three places read it and must not
/// grow their own copy: the board queries (which hide a lapsed request), the
/// atomic accept (which refuses one), and the lifecycle sweeper (which writes
/// the tombstone).
///
/// Constants for now; making them admin-configurable per city is the same open
/// TODO that <see cref="DispatchRadius"/> carries.
/// </summary>
public static class TripExpiry
{
    /// <summary>How long each search window lasts, at booking and per extension.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(3);

    /// <summary>
    /// How long a lapsed request waits for the rider before it is written off.
    /// Long enough to notice a push and answer; short enough not to be its own
    /// version of hanging forever.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How many times a rider may extend. Caps the whole search at
    /// <see cref="Window"/> × (1 + this) plus <see cref="Grace"/> — nine minutes
    /// of searching, ten before the trip is certainly closed. A product guess,
    /// not a technical limit.
    /// </summary>
    public const int MaxExtensions = 2;

    /// <summary>The deadline a freshly booked request gets.</summary>
    public static DateTime InitialDeadline(DateTime nowUtc) => nowUtc + Window;

    /// <summary>
    /// The deadline after an extension — measured from the tap, not from the
    /// deadline that just passed, so a rider who answers at 3:40 gets a whole
    /// window rather than the twenty seconds left of the old one.
    /// </summary>
    public static DateTime ExtendedDeadline(DateTime nowUtc) => nowUtc + Window;

    /// <summary>
    /// True once the search window has closed: off the boards and unacceptable,
    /// though still extendable until <see cref="IsPastGrace"/>.
    /// </summary>
    public static bool IsPaused(Trip trip, DateTime nowUtc) =>
        trip.Status == TripStatus.Requested && trip.ExpiresAtUtc <= nowUtc;

    /// <summary>True once even the grace period has run out — the sweeper's cue to close the trip.</summary>
    public static bool IsPastGrace(Trip trip, DateTime nowUtc) =>
        trip.Status == TripStatus.Requested && trip.ExpiresAtUtc + Grace <= nowUtc;

    /// <summary>
    /// Whether the rider can still extend: the trip is open, has extensions
    /// left, and hasn't run out its grace. Computed here so the server owns the
    /// rule and clients only render the answer.
    /// </summary>
    public static bool CanExtend(Trip trip, DateTime nowUtc) =>
        trip.Status == TripStatus.Requested
        && trip.ExtensionCount < MaxExtensions
        && !IsPastGrace(trip, nowUtc);
}
