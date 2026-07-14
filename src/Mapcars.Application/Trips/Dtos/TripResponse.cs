namespace Mapcars.Application.Trips.Dtos;

/// <summary>
/// Outbound trip representation. Never expose entities directly. Money fields are
/// GBP; <c>DriverEarnings</c>/<c>PlatformFeeAmount</c> feed the driver app's
/// earnings breakdown.
/// </summary>
public record TripResponse(
    Guid Id,
    string PickupAddress,
    double PickupLat,
    double PickupLng,
    string DropoffAddress,
    double DropoffLat,
    double DropoffLng,
    string Status,
    decimal? FareAmount,
    string? Tier,
    double? DistanceMiles,
    double? DurationMinutes,
    decimal? SurgeMultiplier,
    decimal? PlatformFeeAmount,
    decimal? DriverEarnings,
    int? FareChartVersion,
    DateTime CreatedAtUtc);
