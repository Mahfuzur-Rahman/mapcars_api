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
    TripCustomerInfo? Customer,
    string CompanyName = "MapCars UK Ltd",
    string CompanyAddress = "Bournemouth & Poole, Dorset, United Kingdom",
    string SupportEmail = "support@mapcars.uk"
)
{
    /// <summary>DEPRECATED alias for <c>Customer</c>, kept for one release so a client
    /// build that predates the rename keeps working. Delete once web and both apps
    /// have shipped. See TODO_PAYMENTS.md phase 1b.</summary>
    public TripCustomerInfo? Rider => Customer;
}
