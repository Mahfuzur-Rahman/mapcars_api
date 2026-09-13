using Mapcars.Domain.Common;
using Mapcars.Domain.Enums;

namespace Mapcars.Domain.Entities;

public class Trip : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid? DriverId { get; set; }
    public Driver? Driver { get; set; }

    public required string PickupAddress { get; set; }
    public double PickupLat { get; set; }
    public double PickupLng { get; set; }

    public required string DropoffAddress { get; set; }
    public double DropoffLat { get; set; }
    public double DropoffLng { get; set; }

    public TripStatus Status { get; set; } = TripStatus.Requested;

    /// <summary>
    /// 4-digit meet-up code, generated at booking. The customer reads it out at the
    /// kerb and the driver confirms it before starting the trip. Null on trips
    /// booked before this existed — clients treat that as "nothing to confirm".
    /// </summary>
    public string? Pin { get; set; }

    /// <summary>Final fare in GBP (incl. VAT). Priced at booking from the fare chart.</summary>
    public decimal? FareAmount { get; set; }

    /// <summary>
    /// Optional tip the customer adds at booking to attract drivers (broadcast model).
    /// Paid on top of the fare and passed 100% to the driver — no commission.
    /// </summary>
    public decimal TipAmount { get; set; }

    // ─── Pricing snapshot (set at booking; see Application/Pricing) ──────────────
    // Captured at booking time so the fare is auditable and independent of later
    // fare-chart edits. Money in GBP (NUMERIC(10,2)); distance in miles.

    /// <summary>Chosen ride tier id: "economy" | "comfort" | "xl" | "premium".</summary>
    public string? Tier { get; set; }

    /// <summary>Route distance used for pricing, in miles.</summary>
    public double? DistanceMiles { get; set; }

    /// <summary>Route duration used for pricing, in minutes.</summary>
    public double? DurationMinutes { get; set; }

    /// <summary>Combined surge multiplier applied (1.0 = no surge).</summary>
    public decimal? SurgeMultiplier { get; set; }

    /// <summary>Platform (MAP CARS) fee in GBP, deducted from the fare.</summary>
    public decimal? PlatformFeeAmount { get; set; }

    /// <summary>Driver take-home in GBP (FareAmount − PlatformFeeAmount + tips).</summary>
    public decimal? DriverEarnings { get; set; }

    /// <summary>Version of the fare chart the fare was priced against.</summary>
    public int? FareChartVersion { get; set; }

    // ─── Payment ──────────────────────────────────────────────────────────────

    /// <summary>How the customer pays. Defaults to cash (settled in person, no charge).</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    /// <summary>Settlement state of the fare. Cash: Pending at booking → Collected on completion.</summary>
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    /// <summary>When the fare was settled (cash collected / card captured). Null until paid.</summary>
    public DateTime? PaidAtUtc { get; set; }

    // ─── Search window (open requests only) ───────────────────────────────────

    /// <summary>
    /// When the current search window closes. The server owns this value and
    /// clients count down to it — never to a deadline they worked out from
    /// their own clock, which on a driver's phone can be minutes out.
    ///
    /// Once it passes, the request is <i>paused</i>: it comes off every driver's
    /// board and can no longer be accepted, but the trip is still
    /// <see cref="TripStatus.Requested"/> and the customer may still extend it.
    /// Paused is derived from this timestamp rather than stored, so it takes
    /// effect the instant the deadline passes instead of on the next sweep.
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// How many times the customer has extended the search (0…<c>TripExpiry.MaxExtensions</c>).
    /// Bounds how long a trip can sit holding a fare priced at booking, since
    /// surge and driver supply both move underneath it.
    /// </summary>
    public int ExtensionCount { get; set; }

    // ─── Lifecycle / cancellation ─────────────────────────────────────────────

    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelledReason { get; set; }

    /// <summary>Set only when a driver cancels after arriving because the customer never showed up.</summary>
    public bool IsNoShow { get; set; }
}
