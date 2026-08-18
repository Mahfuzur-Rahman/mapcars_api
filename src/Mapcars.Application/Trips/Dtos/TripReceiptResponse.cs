namespace Mapcars.Application.Trips.Dtos;

/// <summary>
/// Authoritative receipt details for a completed or historical trip.
/// </summary>
public record TripReceiptResponse(
    Guid TripId,
    string ReceiptNumber,
    string Status,
    string PickupAddress,
    double PickupLat,
    double PickupLng,
    string DropoffAddress,
    double DropoffLat,
    double DropoffLng,
    string Tier,
    double? DistanceMiles,
    double? DurationMinutes,
    decimal FareAmount,
    decimal TipAmount,
    decimal TotalAmount,
    string PaymentMethod,
    string PaymentStatus,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? PaidAtUtc,
    TripDriverInfo? Driver,
    TripRiderInfo? Rider,
    string CompanyName = "MapCars UK Ltd",
    string CompanyAddress = "Bournemouth & Poole, Dorset, United Kingdom",
    string SupportEmail = "support@mapcars.uk"
);
