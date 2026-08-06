namespace Mapcars.Application.Geo.Models;

/// <summary>Store-level nearby-driver result (id as a Guid; the API maps it to a DTO).</summary>
public record NearbyDriver(Guid DriverId, double Lat, double Lng, double DistanceMeters, double? Heading = null);
