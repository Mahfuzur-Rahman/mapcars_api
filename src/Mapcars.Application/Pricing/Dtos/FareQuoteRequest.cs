namespace Mapcars.Application.Pricing.Dtos;

/// <summary>
/// Inbound request to price a route across all tiers. The client supplies the
/// route it already fetched for the map (distance/duration); the API clamps those
/// against a straight-line sanity check before pricing, so a tampered distance
/// can't inflate the fare.
/// </summary>
public record FareQuoteRequest(
    double PickupLat,
    double PickupLng,
    double DropoffLat,
    double DropoffLng,
    double DistanceMiles,
    double DurationMinutes);
