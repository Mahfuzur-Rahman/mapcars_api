namespace Mapcars.Domain.Enums;

/// <summary>
/// Trip lifecycle. Transitions are enforced by the domain/application layer
/// (e.g. you cannot go straight from Requested to Completed).
/// </summary>
public enum TripStatus
{
    Requested = 0,
    DriverAssigned = 1,
    DriverArrived = 2,
    InProgress = 3,
    Completed = 4,
    CancelledByRider = 5,
    CancelledByDriver = 6,

    /// <summary>
    /// Nobody accepted the request before its search window ran out (see
    /// <c>Application/Dispatch/TripExpiry</c>). Terminal, and distinct from
    /// <see cref="CancelledByRider"/> on purpose: a customer who taps "cancel"
    /// walked away, while this is the platform failing to find anyone — and
    /// only one of those is a supply problem worth measuring.
    /// </summary>
    Expired = 7
}
