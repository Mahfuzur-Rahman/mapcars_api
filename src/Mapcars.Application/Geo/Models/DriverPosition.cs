namespace Mapcars.Application.Geo.Models;

/// <summary>
/// One driver's last known position, as held in the live store. Unlike
/// <see cref="NearbyDriver"/> there's no distance (there's no query point) but
/// there <i>is</i> an <paramref name="UpdatedAtUtc"/>: this is served to a rider
/// tracking a specific driver, who needs to know whether the dot on their map is
/// live or the last thing we heard before the driver's phone lost signal.
/// </summary>
public record DriverPosition(double Lat, double Lng, double? Heading, DateTime UpdatedAtUtc);
