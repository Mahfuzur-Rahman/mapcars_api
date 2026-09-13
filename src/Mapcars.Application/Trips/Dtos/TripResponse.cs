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
    decimal TipAmount,
    string? Tier,
    double? DistanceMiles,
    double? DurationMinutes,
    decimal? SurgeMultiplier,
    decimal? PlatformFeeAmount,
    decimal? DriverEarnings,
    int? FareChartVersion,
    string PaymentMethod,
    string PaymentStatus,
    DateTime? PaidAtUtc,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancelledReason,
    bool IsNoShow,
    TripDriverInfo? Driver,
    TripRiderInfo? Rider = null,
    string? Pin = null,

    /// <summary>
    /// When the current search window closes. Both apps count down to this
    /// value; they must not compute a deadline from their own clock, which on a
    /// driver's phone can be minutes out. Only meaningful while the trip is
    /// still <c>Requested</c> — on a trip that already ended it is simply the
    /// deadline it never reached.
    /// </summary>
    DateTime? ExpiresAtUtc = null,

    /// <summary>How many times the rider has already extended the search.</summary>
    int ExtensionCount = 0,

    /// <summary>
    /// Whether the rider may extend right now. Computed server-side on purpose:
    /// clients render this, they don't re-derive it from
    /// <see cref="ExtensionCount"/>, so the rule lives in exactly one place.
    /// </summary>
    bool CanExtend = false);

/// <summary>
/// The assigned driver's public details, shown on the rider's tracking card.
/// Only populated once a driver is assigned (null before then). Deliberately
/// excludes phone/contact details — the in-app "Call"/"Message" actions don't
/// need the raw number client-side.
/// </summary>
public record TripDriverInfo(
    string Name,
    decimal? Rating,
    string? Vehicle,
    string? Plate);

/// <summary>
/// The rider's public details, shown on the assigned driver's pickup/arrived
/// screens so they know who they're collecting. Only populated for the trip's
/// own two parties — never on the open dispatch board, where broadcasting a
/// rider's name to every nearby driver would leak it to people who never take
/// the trip. Like <see cref="TripDriverInfo"/>, deliberately excludes phone.
/// </summary>
public record TripRiderInfo(
    string Name,
    decimal? Rating);

/// <summary>Cancel a trip. <c>IsNoShow</c> is only honoured for a driver cancelling after arriving.</summary>
public record CancelTripRequest(string? Reason, bool IsNoShow = false);
