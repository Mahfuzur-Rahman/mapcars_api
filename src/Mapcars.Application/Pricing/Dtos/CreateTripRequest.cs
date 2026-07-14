namespace Mapcars.Application.Pricing.Dtos;

/// <summary>
/// Inbound payload to book a trip. The rider identity comes from the JWT, never
/// the body. The API re-prices the chosen tier from the current fare chart (using
/// the clamped route metrics) — the client never sends the price.
/// </summary>
public record CreateTripRequest(
    string PickupAddress,
    double PickupLat,
    double PickupLng,
    string DropoffAddress,
    double DropoffLat,
    double DropoffLng,
    string RideOptionId,
    double DistanceMiles,
    double DurationMinutes,
    string? PromoCode,
    string? PaymentMethodId);
