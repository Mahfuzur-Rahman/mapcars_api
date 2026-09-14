using Mapcars.Application.Common.Exceptions;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Dispatch.Interfaces;
using Mapcars.Application.Dispatch.Services;
using Mapcars.Application.Dispatch;
using Mapcars.Application.Drivers.Interfaces;
using Mapcars.Application.Drivers;
using Mapcars.Application.Notifications.Dtos;
using Mapcars.Application.Notifications.Interfaces;
using Mapcars.Application.Pricing.Dtos;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Pricing;
using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Application.Customers.Interfaces;
using Mapcars.Application.Settings;
using Mapcars.Application.Settings.Interfaces;
using Mapcars.Application.Settings.Models;
using Mapcars.Application.Trips.Dtos;
using Mapcars.Application.Trips.Interfaces;
using Mapcars.Application.Trips.Mapping;
using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Domain.Constants;
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
    private readonly ICustomerRepository _customers;
    private readonly IDriverRepository _drivers;
    private readonly IVehicleRepository _vehicles;
    private readonly IPricingService _pricing;
    private readonly IUnitOfWork _uow;
    private readonly ITripNotifier _notifier;
    private readonly IDispatchService _dispatch;
    private readonly IPushService _push;
    private readonly ISettingsStore _settings;

    public TripService(
        ITripRepository trips,
        ICustomerRepository customers,
        IDriverRepository drivers,
        IVehicleRepository vehicles,
        IPricingService pricing,
        IUnitOfWork uow,
        ITripNotifier notifier,
        IDispatchService dispatch,
        IPushService push,
        ISettingsStore settings)
    {
        _trips = trips;
        _customers = customers;
        _drivers = drivers;
        _vehicles = vehicles;
        _pricing = pricing;
        _uow = uow;
        _notifier = notifier;
        _dispatch = dispatch;
        _push = push;
        _settings = settings;
    }

    public async Task<IReadOnlyList<TripResponse>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default)
    {
        var trips = await _trips.ListForCustomerAsync(customerId, ct);
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
        Guid driverId, double lat, double lng, double? radiusMeters = null, CancellationToken ct = default)
    {
        await EnsureCanReceiveRequestsAsync(driverId, ct);
        var vehicle = await _vehicles.GetByDriverAsync(driverId, ct);

        // Filter the board by payment method as well as tier. Without this a
        // card-only driver would watch cash jobs appear and then be refused on
        // accept, which reads as the platform being broken rather than as a
        // setting on their account.
        var driver = await _drivers.GetByIdAsync(driverId, ct);
        var payments = await _settings.GetAsync<PaymentSettings>(SettingKeys.Payments, ct);

        var now = DateTime.UtcNow;
        var trips = await _trips.ListAvailableAsync(ct);
        return trips
            .Where(t => vehicle is null || DispatchService.IsTierCompatible(vehicle.Tier, t.Tier))
            .Where(t => driver is null || DriverPaymentOptions.Accepts(payments, driver, t.PaymentMethod))
            .Select(t => (trip: t, meters: FareCalculator.HaversineMeters(lat, lng, t.PickupLat, t.PickupLng)))
            // Each request reaches as far as its own age allows — the same rule
            // the push obeys, so a job broadcast to a distant driver survives
            // that driver's next poll instead of being deleted off their board.
            .Where(x => x.meters <= DispatchRadius.For(x.trip, now))
            // An explicit query radius is only ever a *narrower* view on top.
            .Where(x => radiusMeters is null || x.meters <= radiusMeters)
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

    /// <summary>
    /// The accept-side half of the payment-method rule.
    ///
    /// <para>
    /// <c>DispatchService.BroadcastAsync</c> already keeps a cash job off a
    /// card-only driver's board, and so does the board query — but a board is a
    /// cache. A driver may still be holding a card pushed before an admin changed
    /// their options. Checked BEFORE the atomic claim, so a driver who cannot take
    /// the fare never assigns it and then has to be unwound.
    /// </para>
    /// </summary>
    private async Task EnsureCanTakePaymentMethodAsync(Guid driverId, Guid tripId, CancellationToken ct)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);
        var driver = await _drivers.GetByIdAsync(driverId, ct) ?? throw new NotFoundException("Driver", driverId);
        var payments = await _settings.GetAsync<PaymentSettings>(SettingKeys.Payments, ct);

        if (!DriverPaymentOptions.Accepts(payments, driver, trip.PaymentMethod))
            throw new DomainException(DriverPaymentOptions.BlockedMessage(trip.PaymentMethod));
    }

    public async Task<TripResponse> CreateAsync(Guid customerId, CreateTripRequest req, CancellationToken ct = default)
    {
        // Authoritative price for the chosen tier (route metrics are clamped inside).
        var fare = await _pricing.PriceTierAsync(
            req.RideOptionId,
            req.PickupLat, req.PickupLng, req.DropoffLat, req.DropoffLng,
            req.DistanceMiles, req.DurationMinutes, ct);

        var chart = await _pricing.GetChartAsync(ct);

        var trip = new Trip
        {
            CustomerId = customerId,
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

            // The search window. Set here rather than defaulted on the entity so
            // there is one obvious place where a request's deadline begins.
            ExpiresAtUtc = TripExpiry.InitialDeadline(DateTime.UtcNow),
        };

        await _trips.AddAsync(trip, ct);
        await _uow.SaveChangesAsync(ct);

        // Broadcast the open request to all nearby drivers' boards (best-effort —
        // a broadcast hiccup never fails the booking; drivers also poll the board).
        try
        {
            await _dispatch.BroadcastAsync(trip, ct: ct);
        }
        catch
        {
            /* realtime broadcast is non-critical to booking */
        }

        // The customer is a party to their own trip, so they get the meet-up PIN
        // straight back from booking — it's on their tracking screen before a
        // driver is even assigned.
        return trip.ToResponse(customer: await BuildCustomerInfoAsync(trip.CustomerId, ct), includePin: true);
    }

    /// <summary>
    /// A 4-digit meet-up code. Not a security token — it only has to be
    /// unguessable enough that someone who pulled up at the same kerb can't
    /// recite it, and short enough to read out loud.
    /// </summary>
    private static string NewPin() => Random.Shared.Next(1000, 10000).ToString();

    /// <summary>Wrong PINs allowed before verification is refused for this trip.</summary>
    private const int MaxPinAttempts = 5;

    public async Task<TripResponse> ExtendAsync(
        Guid customerId, Guid tripId, CancellationToken ct = default)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        // Same treatment as GetForUserAsync: a trip belonging to someone else is
        // "not found", never "forbidden", so this can't be used to probe ids.
        if (trip.CustomerId != customerId) throw new NotFoundException("Trip", tripId);

        if (trip.Status != TripStatus.Requested)
            throw new DomainException("This ride is no longer searching for a driver.");

        if (trip.ExtensionCount >= TripExpiry.MaxExtensions)
            throw new DomainException("You've already extended this search as far as it goes.");

        // Past the grace period the sweeper has closed it, or is about to. Letting
        // an extension resurrect it would put a request back on the boards after
        // the customer was told it was over.
        if (TripExpiry.IsPastGrace(trip, DateTime.UtcNow))
            throw new DomainException("This search has already ended — please book again.");

        trip.ExpiresAtUtc = TripExpiry.ExtendedDeadline(DateTime.UtcNow);
        trip.ExtensionCount++;
        await _uow.SaveChangesAsync(ct);

        // Straight to the widest ring. The trip is well past DispatchRadius.MaxAfter
        // by now, so this is the radius the age rule would pick anyway — passing it
        // explicitly says the intent out loud. Best-effort, like the booking
        // broadcast: drivers also poll, and the extension has already been saved.
        try { await _dispatch.BroadcastAsync(trip, DispatchRadius.MaxMeters, ct); }
        catch { /* realtime broadcast is non-critical to the extension */ }

        return await NotifiedAsync(trip, ct);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public async Task<TripResponse> AcceptAsync(Guid driverId, Guid tripId, CancellationToken ct = default)
    {
        await EnsureCanReceiveRequestsAsync(driverId, ct);
        await EnsureCanTakePaymentMethodAsync(driverId, tripId, ct);

        // Broadcast model — first-come wins. The atomic Requested→DriverAssigned is
        // the single guard against two drivers grabbing the same trip; false means
        // someone beat us to it (or it was cancelled).
        if (!await _trips.TryAssignAsync(tripId, driverId, ct))
            throw new DomainException("This trip has already been taken.");

        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        // Drop it off every other nearby driver's board — best-effort, a
        // realtime hiccup here must never fail the accept that already succeeded.
        try { await _dispatch.WithdrawAsync(trip, ct: ct); }
        catch { /* non-critical */ }

        return await NotifiedAsync(trip, ct);
    }

    public async Task<TripResponse> ArriveAsync(Guid driverId, Guid tripId, CancellationToken ct = default)
        => await TransitionAsync(driverId, tripId, TripStatus.DriverAssigned, TripStatus.DriverArrived, ct);

    public async Task<TripResponse> StartAsync(
        Guid driverId, Guid tripId, string? pin = null, CancellationToken ct = default)
    {
        await VerifyPinAsync(driverId, tripId, pin, ct);
        return await TransitionAsync(driverId, tripId, TripStatus.DriverArrived, TripStatus.InProgress, ct);
    }

    /// <summary>
    /// Check the kerbside PIN, server-side.
    ///
    /// <para>
    /// Until now this was checked only in the driver app, which means it proved
    /// nothing: a driver calling the API directly skipped it, and the server kept
    /// no record either way. A client-side check is a UX affordance, not a
    /// control.
    /// </para>
    ///
    /// <para>
    /// A missing PIN is allowed through (older driver builds send none) but
    /// leaves <c>PinVerifiedAtUtc</c> null. A WRONG one is refused — sending a
    /// guess is not something an honest app does.
    /// </para>
    /// </summary>
    private async Task VerifyPinAsync(Guid driverId, Guid tripId, string? pin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pin)) return;

        var trip = await GetOwnedByDriverAsync(driverId, tripId, ct);
        if (string.IsNullOrEmpty(trip.Pin)) return;      // booked before PINs existed
        if (trip.PinVerifiedAtUtc is not null) return;   // already proved

        // 10,000 combinations is nothing over an API. Cap the attempts, or a
        // driver could brute-force a "verified" pickup that never happened.
        if (trip.PinAttemptCount >= MaxPinAttempts)
            throw new DomainException(
                "Too many incorrect PIN attempts. Ask the customer to confirm their booking, " +
                "or contact support.");

        if (!FixedTimeEquals(trip.Pin, pin.Trim()))
        {
            trip.PinAttemptCount++;
            await _uow.SaveChangesAsync(ct);
            throw new DomainException("That PIN doesn't match. Ask the customer to read it out again.");
        }

        trip.PinVerifiedAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Length-independent, content-constant-time comparison. A 4-digit secret
    /// compared with ordinary string equality leaks its prefix through timing,
    /// and the cost of not leaking it is a few nanoseconds.
    /// </summary>
    private static bool FixedTimeEquals(string expected, string actual)
    {
        var diff = expected.Length ^ actual.Length;
        for (var i = 0; i < expected.Length && i < actual.Length; i++)
            diff |= expected[i] ^ actual[i];
        return diff == 0;
    }

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

        var isCustomer = callerType == UserTypes.Customer && trip.CustomerId == callerId;
        var isDriver = callerType == "driver" && trip.DriverId == callerId;
        if (!isCustomer && !isDriver)
            throw new NotFoundException("Trip", tripId);

        if (trip.Status is TripStatus.Completed or TripStatus.CancelledByCustomer or TripStatus.CancelledByDriver)
            throw new DomainException("This trip can no longer be cancelled.");

        // No-show only makes sense for a driver who actually arrived and waited.
        var isNoShow = isDriver && request.IsNoShow && trip.Status == TripStatus.DriverArrived;

        trip.Status = isCustomer ? TripStatus.CancelledByCustomer : TripStatus.CancelledByDriver;
        trip.CancelledAtUtc = DateTime.UtcNow;
        trip.CancelledReason = request.Reason;
        trip.IsNoShow = isNoShow;

        // A cancelled trip owes nothing and never will. Leaving it Pending - which
        // is what happened until now - reads as an unsettled fare in every
        // "what is outstanding?" query and on the admin transactions view.
        // Only Pending moves: a fare already Collected (a cash trip cancelled
        // after drop-off, however that happened) must not be quietly unsettled.
        if (trip.PaymentStatus == PaymentStatus.Pending)
            trip.PaymentStatus = PaymentStatus.Voided;

        if (isCustomer)
        {
            var customer = await _customers.GetByIdAsync(callerId, ct);
            if (customer is not null) customer.CancellationCount++;
        }
        else
        {
            var driver = await _drivers.GetByIdAsync(callerId, ct);
            if (driver is not null) driver.CancellationCount++;
        }

        if (isNoShow)
        {
            var customer = await _customers.GetByIdAsync(trip.CustomerId, ct);
            if (customer is not null) customer.NoShowCount++;
        }

        await _uow.SaveChangesAsync(ct);

        // If this was still an open, unassigned request, it was on nearby
        // drivers' boards — pull it off theirs too (best-effort).
        if (trip.DriverId is null)
        {
            try { await _dispatch.WithdrawAsync(trip, ct: ct); }
            catch { /* non-critical */ }
        }

        return await NotifiedAsync(trip, ct);
    }

    public async Task<TripResponse> GetForUserAsync(
        string callerType, Guid callerId, Guid tripId, CancellationToken ct = default)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        var isCustomer = callerType == UserTypes.Customer && trip.CustomerId == callerId;
        var isDriver = callerType == "driver" && trip.DriverId == callerId;
        if (!isCustomer && !isDriver) throw new NotFoundException("Trip", tripId); // don't leak others' trips

        var driver = await BuildDriverInfoAsync(trip.DriverId, ct);
        var customer = await BuildCustomerInfoAsync(trip.CustomerId, ct);
        return trip.ToResponse(driver, customer, includePin: true);
    }

    public async Task<TripResponse?> GetActiveForUserAsync(
        string callerType, Guid callerId, CancellationToken ct = default)
    {
        var trip = callerType == "driver"
            ? await _trips.GetActiveForDriverAsync(callerId, ct)
            : await _trips.GetActiveForCustomerAsync(callerId, ct);

        if (trip is null) return null;

        var driver = await BuildDriverInfoAsync(trip.DriverId, ct);
        var customer = await BuildCustomerInfoAsync(trip.CustomerId, ct);
        return trip.ToResponse(driver, customer, includePin: true);
    }

    public async Task<TripReceiptResponse> GetReceiptForUserAsync(
        string callerType, Guid callerId, Guid tripId, CancellationToken ct = default)
    {
        var trip = await _trips.GetByIdAsync(tripId, ct) ?? throw new NotFoundException("Trip", tripId);

        var isCustomer = callerType == UserTypes.Customer && trip.CustomerId == callerId;
        var isDriver = callerType == "driver" && trip.DriverId == callerId;
        var isAdmin = callerType == "admin" || callerType == "SuperAdmin" || callerType == "Operations" || callerType == "Support";
        if (!isCustomer && !isDriver && !isAdmin) throw new NotFoundException("Trip", tripId);

        var driver = await BuildDriverInfoAsync(trip.DriverId, ct);
        var customer = await BuildCustomerInfoAsync(trip.CustomerId, ct);

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
            customer
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Looks up the assigned driver's public details for the customer's
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

    /// <summary>Looks up the customer's public details for the assigned driver's
    /// pickup card — who they're collecting.</summary>
    private async Task<TripCustomerInfo?> BuildCustomerInfoAsync(Guid customerId, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(customerId, ct);
        if (customer is null) return null;

        return new TripCustomerInfo(customer.FullName ?? "Your customer", customer.AverageRating);
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
        var customer = await BuildCustomerInfoAsync(trip.CustomerId, ct);

        // The trip group only ever contains this trip's customer and its assigned
        // driver (TripHub.JoinTrip enforces that), so both parties' details and
        // the PIN are safe to carry on this response.
        var response = trip.ToResponse(driver, customer, includePin: true);
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
                await NotifyCustomerAsync(trip, "Driver on the way",
                    "A driver accepted your trip and is heading to you.", ct);
                break;
            case TripStatus.DriverArrived:
                await NotifyCustomerAsync(trip, "Your driver has arrived",
                    "Head out to meet your driver.", ct);
                break;
            case TripStatus.Completed:
                await NotifyCustomerAsync(trip, "Trip complete",
                    "Thanks for riding with MAP CARS.", ct);
                break;
            case TripStatus.CancelledByDriver:
                await NotifyCustomerAsync(trip, "Trip cancelled",
                    "Your driver cancelled the trip.", ct);
                break;
            case TripStatus.CancelledByCustomer when trip.DriverId is Guid driverId:
                await _push.NotifyUserAsync("driver", driverId,
                    new PushMessage("Trip cancelled", "The customer cancelled the trip.", TripData(trip)), ct);
                break;
        }
    }

    private Task NotifyCustomerAsync(Trip trip, string title, string body, CancellationToken ct)
        => _push.NotifyUserAsync(UserTypes.Customer, trip.CustomerId, new PushMessage(title, body, TripData(trip)), ct);

    private static IReadOnlyDictionary<string, string> TripData(Trip trip) => new Dictionary<string, string>
    {
        ["tripId"] = trip.Id.ToString(),
        ["status"] = trip.Status.ToString(),
    };

    private static decimal Gbp(int pence) => pence / 100m;
}
