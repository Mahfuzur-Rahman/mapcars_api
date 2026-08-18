using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Dispatch.Interfaces;
using Mapcars.Application.Dispatch.Services;
using Mapcars.Application.Drivers;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Notifications.Dtos;
using Mapcars.Application.Notifications.Interfaces;
using Mapcars.Application.Pricing;
using Mapcars.Application.Pricing.Dtos;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Application.Riders.Interfaces;
using Mapcars.Application.Trips.Dtos;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Application.Trips.Mapping;
using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Trips.Services;

/// <summary>
/// Business logic for trips. Listing is read-only; booking (<see cref="CreateAsync"/>)
/// prices the chosen tier authoritatively from the fare chart and snapshots the
/// fare rules at that moment. The requests board (available trips) is filtered
/// by driver status and vehicle tier compatibility.
/// </summary>
public class TripService : ITripService
{
    private readonly ITripRepository _trips;
    private readonly IRiderRepository _riders;
    private readonly IDriverRepository _drivers;
    private readonly IVehicleRepository _vehicles;
    private readonly IPricingService _pricing;
    private readonly IUnitOfWork _uow;
    private readonly ITripNotifier _notifier;
    private readonly IDispatchService _dispatch;
    private readonly IPushService _push;

    public TripService(
        ITripRepository trips,
        IRiderRepository riders,
        IDriverRepository drivers,
        IVehicleRepository vehicles,
        IPricingService pricing,
        IUnitOfWork uow,
        ITripNotifier notifier,
        IDispatchService dispatch,
        IPushService push)
    {
        _trips = trips;
        _riders = riders;
        _drivers = drivers;
        _vehicles = vehicles;
        _pricing = pricing;
        _uow = uow;
        _notifier = notifier;
        _dispatch = dispatch;
        _push = push;
    }

    public async Task<IReadOnlyList<TripResponse>> ListForRiderAsync(Guid riderId, CancellationToken ct = default)
    {
        var trips = await _trips.ListForRiderAsync(riderId, ct);
        return trips.Select(t => t.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<TripResponse>> ListForDriverAsync(Guid driverId, CancellationToken ct = default)
    {
        var trips = await _trips.ListForDriverAsync(driverId, ct);
        return trips.Select(t => t.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<TripResponse>> ListAvailableAsync(Guid driverId, CancellationToken ct = default)
    {
        await EnsureCanReceiveRequestsAsync(driverId, ct);
        var vehicle = await _vehicles.GetByDriverAsync(driverId, ct);

        var trips = await _trips.ListAvailableAsync(ct);
        return trips
            .Where(t => vehicle is null || DispatchService.IsTierCompatible(vehicle.Tier, t.Tier))
            .Select(t => t.ToResponse())
            .ToList();
    }

    public async Task<IReadOnlyList<TripResponse>> ListAvailableNearbyAsync(
        Guid driverId, double lat, double lng, double radiusMeters, CancellationToken ct = default)
    {
        await EnsureCanReceiveRequestsAsync(driverId, ct);
        var vehicle = await _vehicles.GetByDriverAsync(driverId, ct);

        var trips = await _trips.ListAvailableAsync(ct);
        return trips
            .Where(t => vehicle is null || DispatchService.IsTierCompatible(vehicle.Tier, t.Tier))
            .Select(t => (trip: t, meters: FareCalculator.HaversineMeters(lat, lng, t.PickupLat, t.PickupLng)))
            .Where(x => x.meters <= radiusMeters)
            .OrderBy(x => x.meters)
            .Select(x => x.trip.ToResponse())
            .ToList();
    }

    /// <summary>
    /// The requests board is only visible to a driver an admin has approved and
    /// who is currently online. This mirrors the accept guard, so an unapproved
    /// driver can't even see the work, let alone take it.
    /// </summary>
    private async Task EnsureCanReceiveRequestsAsync(Guid driverId, CancellationToken ct)
    {
        var driver = await _drivers.GetByIdAsync(driverId, ct) ?? throw new NotFoundException("Driver", driverId);

        if (!DriverApproval.CanWork(driver))
            throw new DomainException(DriverApproval.BlockedMessage(driver.Status));

        if (!driver.IsOnline)
            throw new DomainException("Go online to see trip requests.");
    }

    public async Task<TripResponse> CreateAsync(Guid riderId, CreateTripRequest req, CancellationToken ct = default)
    {
        // Authoritative price for the chosen tier (route metrics are clamped inside).
        var fare = await _pricing.PriceTierAsync(
            req.RideOptionId,
            req.PickupLat, req.PickupLng, req.DropoffLat, req.DropoffLng,
            req.DistanceMiles, req.DurationMinutes, ct);

        var chart = await _pricing.GetChartAsync(ct);

        var trip = new Trip
        {
            RiderId = riderId,
            PickupAddress = req.PickupAddress,
            PickupLat = req.PickupLat,
            PickupLng = req.PickupLng,
            DropoffAddress = req.DropoffAddress,
            DropoffLat = req.DropoffLat,
            DropoffLng = req.DropoffLng,
            Status = Domain.Enums.TripStatus.Requested,
            Pin = NewPin(),

            Tier = fare.TierId,
            DistanceMiles = req.DistanceMiles,
            DurationMinutes = req.DurationMinutes,
            SurgeMultiplier = fare.SurgeMultiplier,
            FareAmount = Gbp(fare.FarePence),
            TipAmount = req.TipAmount < 0 ? 0 : req.TipAmount,
            PlatformFeeAmount = Gbp(fare.PlatformFeePence),
            DriverEarnings = Gbp(fare.DriverEarningsPence),
            FareChartVersion = chart.Version,

            // Payment: cash by default (settled in person on completion — no charge).
            // Card is accepted here but not yet charged; Stripe capture lands next.
            PaymentMethod = ParsePaymentMethod(req.PaymentMethod),
            PaymentStatus = PaymentStatus.Pending,
        };

        await _trips.AddAsync(trip, ct);
        await _uow.SaveChangesAsync(ct);

        // Broadcast the open request to all nearby drivers' boards (best-effort —
        // a broadcast hiccup never fails the booking; drivers also poll the board).
        try
        {
            await _dispatch.BroadcastAsync(trip, ct);
        }
        catch
        {
            /* realtime broadcast is non-critical to booking */
        }

        // The rider is a party to their own trip, so they get the meet-up PIN
        // straight back from booking — it's on their tracking screen before a
        // driver is even assigned.
        return trip.ToResponse(rider: await BuildRiderInfoAsync(trip.RiderId, ct), includePin: true);
    }

    /// <summary>
    /// A 4-digit meet-up code. Not a security token — it only has to be
    /// unguessable enough that someone who pulled up at the same kerb can't
    /// recite it, and short enough to read out loud.
    /// </summary>
    private static string NewPin() => Random.Shared.Next(1000, 10000).ToString();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public async Task<TripResponse> AcceptAsync(Guid driverId, Guid tripId, CancellationToken ct = default)
    {
        await EnsureCanReceiveRequestsAsync(driverId, ct);

        // Broadcast model — first-come wins. The atomic Requested→DriverAssigned is
        // the single guard against two drivers grabbing the same trip; false means
        // someone beat us to it (or it was cancelled).
        if (!await _trips.TryAssignAsync(tripId, driverId, ct))
            throw new DomainException("This trip has already been taken.");

        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        // Drop it off every other nearby driver's board — best-effort, a
        // realtime hiccup here must never fail the accept that already succeeded.
        try { await _dispatch.WithdrawAsync(trip, ct); }
        catch { /* non-critical */ }

        return await NotifiedAsync(trip, ct);
    }

    public async Task<TripResponse> ArriveAsync(Guid driverId, Guid tripId, CancellationToken ct = default)
        => await TransitionAsync(driverId, tripId, TripStatus.DriverAssigned, TripStatus.DriverArrived, ct);

    public async Task<TripResponse> StartAsync(Guid driverId, Guid tripId, CancellationToken ct = default)
        => await TransitionAsync(driverId, tripId, TripStatus.DriverArrived, TripStatus.InProgress, ct);

    public async Task<TripResponse> CompleteAsync(Guid driverId, Guid tripId, CancellationToken ct = default)
    {
        var trip = await GetOwnedByDriverAsync(driverId, tripId, ct);

        if (trip.Status != TripStatus.InProgress)
            throw new DomainException("Only a trip that is in progress can be completed.");

        trip.Status = TripStatus.Completed;
        trip.CompletedAtUtc = DateTime.UtcNow;

        // Cash is settled in person at drop-off — mark it collected now. Card is
        // left Pending here; the Stripe capture step (next) will settle it.
        if (trip.PaymentMethod == PaymentMethod.Cash && trip.PaymentStatus == PaymentStatus.Pending)
        {
            trip.PaymentStatus = PaymentStatus.Collected;
            trip.PaidAtUtc = DateTime.UtcNow;
        }

        await _uow.SaveChangesAsync(ct);
        return await NotifiedAsync(trip, ct);
    }

    /// <summary>Maps the request's payment-method string to the enum (defaults to cash).</summary>
    private static PaymentMethod ParsePaymentMethod(string? raw) =>
        string.Equals(raw, "card", StringComparison.OrdinalIgnoreCase)
            ? PaymentMethod.Card
            : PaymentMethod.Cash;

    public async Task<TripResponse> CancelAsync(
        string callerType, Guid callerId, Guid tripId, CancelTripRequest request, CancellationToken ct = default)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        var isRider = callerType == "rider" && trip.RiderId == callerId;
        var isDriver = callerType == "driver" && trip.DriverId == callerId;
        if (!isRider && !isDriver)
            throw new NotFoundException("Trip", tripId);

        if (trip.Status is TripStatus.Completed or TripStatus.CancelledByRider or TripStatus.CancelledByDriver)
            throw new DomainException("This trip can no longer be cancelled.");

        // No-show only makes sense for a driver who actually arrived and waited.
        var isNoShow = isDriver && request.IsNoShow && trip.Status == TripStatus.DriverArrived;

        trip.Status = isRider ? TripStatus.CancelledByRider : TripStatus.CancelledByDriver;
        trip.CancelledAtUtc = DateTime.UtcNow;
        trip.CancelledReason = request.Reason;
        trip.IsNoShow = isNoShow;

        if (isRider)
        {
            var rider = await _riders.GetByIdAsync(callerId, ct);
            if (rider is not null) rider.CancellationCount++;
        }
        else
        {
            var driver = await _drivers.GetByIdAsync(callerId, ct);
            if (driver is not null) driver.CancellationCount++;
        }

        if (isNoShow)
        {
            var rider = await _riders.GetByIdAsync(trip.RiderId, ct);
            if (rider is not null) rider.NoShowCount++;
        }

        await _uow.SaveChangesAsync(ct);

        // If this was still an open, unassigned request, it was on nearby
        // drivers' boards — pull it off theirs too (best-effort).
        if (trip.DriverId is null)
        {
            try { await _dispatch.WithdrawAsync(trip, ct); }
            catch { /* non-critical */ }
        }

        return await NotifiedAsync(trip, ct);
    }

    public async Task<TripResponse> GetForUserAsync(
        string callerType, Guid callerId, Guid tripId, CancellationToken ct = default)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        var isRider = callerType == "rider" && trip.RiderId == callerId;
        var isDriver = callerType == "driver" && trip.DriverId == callerId;
        if (!isRider && !isDriver) throw new NotFoundException("Trip", tripId); // don't leak others' trips

        var driver = await BuildDriverInfoAsync(trip.DriverId, ct);
        var rider = await BuildRiderInfoAsync(trip.RiderId, ct);
        return trip.ToResponse(driver, rider, includePin: true);
    }

    public async Task<TripResponse?> GetActiveForUserAsync(
        string callerType, Guid callerId, CancellationToken ct = default)
    {
        var trip = callerType == "driver"
            ? await _trips.GetActiveForDriverAsync(callerId, ct)
            : await _trips.GetActiveForRiderAsync(callerId, ct);

        if (trip is null) return null;

        var driver = await BuildDriverInfoAsync(trip.DriverId, ct);
        var rider = await BuildRiderInfoAsync(trip.RiderId, ct);
        return trip.ToResponse(driver, rider, includePin: true);
    }

    public async Task<TripReceiptResponse> GetReceiptForUserAsync(
        string callerType, Guid callerId, Guid tripId, CancellationToken ct = default)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        var isRider = callerType == "rider" && trip.RiderId == callerId;
        var isDriver = callerType == "driver" && trip.DriverId == callerId;
        var isAdmin = callerType == "admin" || callerType == "SuperAdmin" || callerType == "Operations" || callerType == "Support";
        if (!isRider && !isDriver && !isAdmin) throw new NotFoundException("Trip", tripId);

        var driver = await BuildDriverInfoAsync(trip.DriverId, ct);
        var rider = await BuildRiderInfoAsync(trip.RiderId, ct);

        var receiptNumber = $"MC-{trip.Id.ToString()[..8].ToUpperInvariant()}";
        var totalAmount = (trip.FareAmount ?? 0m) + trip.TipAmount;

        return new TripReceiptResponse(
            trip.Id,
            receiptNumber,
            trip.Status.ToString(),
            trip.PickupAddress,
            trip.PickupLat,
            trip.PickupLng,
            trip.DropoffAddress,
            trip.DropoffLat,
            trip.DropoffLng,
            trip.Tier ?? "Economy",
            trip.DistanceMiles,
            trip.DurationMinutes,
            trip.FareAmount ?? 0m,
            trip.TipAmount,
            totalAmount,
            trip.PaymentMethod.ToString(),
            trip.PaymentStatus.ToString(),
            trip.CreatedAtUtc,
            trip.CompletedAtUtc,
            trip.PaidAtUtc,
            driver,
            rider
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Looks up the assigned driver's public details for the rider's
    /// tracking card. Null until a driver is assigned.</summary>
    private async Task<TripDriverInfo?> BuildDriverInfoAsync(Guid? driverId, CancellationToken ct)
    {
        if (driverId is null) return null;
        var driver = await _drivers.GetByIdAsync(driverId.Value, ct);
        if (driver is null) return null;

        var vehicle = await _vehicles.GetByDriverAsync(driverId.Value, ct);
        return new TripDriverInfo(
            driver.FullName ?? "Your driver",
            driver.AverageRating,
            vehicle is null ? null : $"{vehicle.Colour} {vehicle.Make} {vehicle.Model}",
            vehicle?.RegistrationNumber);
    }

    /// <summary>Looks up the rider's public details for the assigned driver's
    /// pickup card — who they're collecting.</summary>
    private async Task<TripRiderInfo?> BuildRiderInfoAsync(Guid riderId, CancellationToken ct)
    {
        var rider = await _riders.GetByIdAsync(riderId, ct);
        if (rider is null) return null;

        return new TripRiderInfo(rider.FullName ?? "Your rider", rider.AverageRating);
    }

    private async Task<Trip> GetOwnedByDriverAsync(Guid driverId, Guid tripId, CancellationToken ct)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct);
        if (trip is null || trip.DriverId != driverId)
            throw new NotFoundException("Trip", tripId);
        return trip;
    }

    private async Task<TripResponse> TransitionAsync(
        Guid driverId, Guid tripId, TripStatus from, TripStatus to, CancellationToken ct)
    {
        var trip = await GetOwnedByDriverAsync(driverId, tripId, ct);

        if (trip.Status != from)
            throw new DomainException($"Trip must be in '{from}' status for this action.");

        trip.Status = to;
        await _uow.SaveChangesAsync(ct);
        return await NotifiedAsync(trip, ct);
    }

    /// <summary>Map a trip to its response and push it to the trip's realtime group.</summary>
    private async Task<TripResponse> NotifiedAsync(Trip trip, CancellationToken ct)
    {
        var driver = await BuildDriverInfoAsync(trip.DriverId, ct);
        var rider = await BuildRiderInfoAsync(trip.RiderId, ct);

        // The trip group only ever contains this trip's rider and its assigned
        // driver (TripHub.JoinTrip enforces that), so both parties' details and
        // the PIN are safe to carry on this response.
        var response = trip.ToResponse(driver, rider, includePin: true);
        await _notifier.TripUpdatedAsync(response, ct);

        // Fire a push to the relevant party for lifecycle transitions that matter
        // when the app is backgrounded. Best-effort (PushService swallows errors).
        await PushForStatusAsync(trip, ct);
        return response;
    }

    /// <summary>
    /// Sends a push for the transitions worth interrupting a user for. The status
    /// itself says who to notify — a <c>CancelledBy*</c> tells us who cancelled, so
    /// we alert the other party.
    /// </summary>
    private async Task PushForStatusAsync(Trip trip, CancellationToken ct)
    {
        switch (trip.Status)
        {
            case TripStatus.DriverAssigned:
                await NotifyRiderAsync(trip, "Driver on the way",
                    "A driver accepted your trip and is heading to you.", ct);
                break;
            case TripStatus.DriverArrived:
                await NotifyRiderAsync(trip, "Your driver has arrived",
                    "Head out to meet your driver.", ct);
                break;
            case TripStatus.Completed:
                await NotifyRiderAsync(trip, "Trip complete",
                    "Thanks for riding with MAP CARS.", ct);
                break;
            case TripStatus.CancelledByDriver:
                await NotifyRiderAsync(trip, "Trip cancelled",
                    "Your driver cancelled the trip.", ct);
                break;
            case TripStatus.CancelledByRider when trip.DriverId is Guid driverId:
                await _push.NotifyUserAsync("driver", driverId,
                    new PushMessage("Trip cancelled", "The rider cancelled the trip.", TripData(trip)), ct);
                break;
        }
    }

    private Task NotifyRiderAsync(Trip trip, string title, string body, CancellationToken ct)
        => _push.NotifyUserAsync("rider", trip.RiderId, new PushMessage(title, body, TripData(trip)), ct);

    private static IReadOnlyDictionary<string, string> TripData(Trip trip) => new Dictionary<string, string>
    {
        ["tripId"] = trip.Id.ToString(),
        ["status"] = trip.Status.ToString(),
    };

    private static decimal Gbp(int pence) => pence / 100m;
}
