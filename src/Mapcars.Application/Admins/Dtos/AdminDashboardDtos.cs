namespace Mapcars.Application.Admins.Dtos;

/// <summary>Headline counts for the admin dashboard. "Today" is UTC-day based.</summary>
public record AdminStatsResponse(
    int TotalCustomers,
    int TotalDrivers,
    int OnlineDrivers,
    int PendingDriverApprovals,
    int ActiveTrips,
    int TripsToday,
    int CompletedTripsToday,
    decimal RevenueTodayGbp)
{
    /// <summary>DEPRECATED alias for <c>TotalCustomers</c>, kept for one release so a client
    /// build that predates the rename keeps working. Delete once web and both apps
    /// have shipped. See TODO_PAYMENTS.md phase 1b.</summary>
    public int TotalRiders => TotalCustomers;
}

/// <summary>A single row in the admin trip-history table.</summary>
public record AdminTripListItem(
    Guid Id,
    string? CustomerName,
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
    DateTime? CancelledAtUtc)
{
    /// <summary>DEPRECATED alias for <c>CustomerName</c>, kept for one release so a client
    /// build that predates the rename keeps working. Delete once web and both apps
    /// have shipped. See TODO_PAYMENTS.md phase 1b.</summary>
    public string? RiderName => CustomerName;
}

/// <summary>An in-flight trip shown on the admin live map (pickup + dropoff points).</summary>
public record AdminActiveTrip(
    Guid Id,
    string Status,
    string? CustomerName,
    string? DriverName,
    string PickupAddress,
    double PickupLat,
    double PickupLng,
    string DropoffAddress,
    double DropoffLat,
    double DropoffLng)
{
    /// <summary>DEPRECATED alias for <c>CustomerName</c>, kept for one release so a client
    /// build that predates the rename keeps working. Delete once web and both apps
    /// have shipped. See TODO_PAYMENTS.md phase 1b.</summary>
    public string? RiderName => CustomerName;
}

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
