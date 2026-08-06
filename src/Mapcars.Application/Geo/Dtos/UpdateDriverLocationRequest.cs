namespace Mapcars.Application.Geo.Dtos;

/// <summary>
/// A driver's current position, pushed periodically while they're online. Kept in
/// Redis GEO (the hot path) — never Postgres. Heading/speed are intentionally
/// omitted from v1; add them (in a companion hash) when the tracking UI needs them.
/// </summary>
/// <param name="TripId">
/// The driver's active trip, if any — when present (and it really is this
/// driver's own assigned/in-progress trip), the position is also relayed to
/// that trip's SignalR group (a <c>driverLocation</c> event) so the rider can
/// watch the car move live.
/// </param>
/// <param name="Heading">Compass heading in degrees (0 = north, clockwise),
/// when the device can supply one — lets nearby-car map markers rotate to
/// face the direction of travel instead of always pointing north.</param>
public record UpdateDriverLocationRequest(double Lat, double Lng, Guid? TripId = null, double? Heading = null);
