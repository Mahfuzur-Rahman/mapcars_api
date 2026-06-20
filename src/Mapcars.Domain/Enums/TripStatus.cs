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
    CancelledByDriver = 6
}
