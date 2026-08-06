namespace Mapcars.Application.Admins.Dtos;

/// <summary>Headline counts for the admin dashboard. "Today" is UTC-day based.</summary>
public record AdminStatsResponse(
    int TotalRiders,
    int TotalDrivers,
    int OnlineDrivers,
    int PendingDriverApprovals,
    int ActiveTrips,
    int TripsToday,
    int CompletedTripsToday,
    decimal RevenueTodayGbp);

/// <summary>A single row in the admin trip-history table.</summary>
public record AdminTripListItem(
    Guid Id,
    string? RiderName,
    string? DriverName,
    string PickupAddress,
    string DropoffAddress,
    string Status,
    string? Tier,
    decimal? FareAmount,
    decimal TipAmount,
    string PaymentMethod,
    string PaymentStatus,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc);

/// <summary>An in-flight trip shown on the admin live map (pickup + dropoff points).</summary>
public record AdminActiveTrip(
    Guid Id,
    string Status,
    string? RiderName,
    string? DriverName,
    string PickupAddress,
    double PickupLat,
    double PickupLng,
    string DropoffAddress,
    double DropoffLat,
    double DropoffLng);

/// <summary>An online driver's live position (from the Redis GEO pool).</summary>
public record AdminOnlineDriver(
    Guid DriverId,
    string? Name,
    double Lat,
    double Lng,
    double? Heading);

/// <summary>Everything the live map renders in one payload.</summary>
public record AdminLiveResponse(
    IReadOnlyList<AdminActiveTrip> ActiveTrips,
    IReadOnlyList<AdminOnlineDriver> OnlineDrivers);
