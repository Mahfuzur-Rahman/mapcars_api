namespace Mapcars.Application.Geo.Dtos;

/// <summary>
/// Where a trip's assigned driver was last seen. This is the cold-start
/// counterpart to the realtime <c>driverLocation</c> push: a rider who opens the
/// app mid-trip has missed every push so far, and would otherwise stare at a map
/// with no car on it until the driver's next 5-second ping — or forever, if the
/// driver's app is backgrounded and reporting has stalled.
/// </summary>
/// <param name="AgeSeconds">
/// How old this fix is. The client uses it to decide whether to show the car as
/// live or dim it as "last seen" rather than implying a stale dot is current.
/// </param>
public record TripDriverLocationResponse(
    double Lat,
    double Lng,
    double? Heading,
    DateTime UpdatedAtUtc,
    int AgeSeconds);
