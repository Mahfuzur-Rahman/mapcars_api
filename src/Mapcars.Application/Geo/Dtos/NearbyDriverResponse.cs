namespace Mapcars.Application.Geo.Dtos;

/// <summary>One nearby online driver, ordered by distance from the query point.</summary>
public record NearbyDriverResponse(string DriverId, double Lat, double Lng, double DistanceMeters, double? Heading = null);
