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

    /// <summary>
    /// When the driver proved the passenger read the PIN out — verified by the
    /// SERVER, not the app.
    ///
    /// <para>
    /// Null means the trip started without proof, which is not blocked but is
    /// recorded. That distinction is the whole value: it is an anti-collusion
    /// signal (a manufactured trip has no passenger to read a code) and the
    /// strongest single piece of evidence in a dispute — "somebody at the kerb
    /// knew a number only the booker was shown".
    /// </para>
    /// </summary>
    public DateTime? PinVerifiedAtUtc { get; set; }

    /// <summary>
    /// Failed PIN attempts. A 4-digit code is 10,000 guesses, which is nothing
    /// over an API, so attempts are capped — otherwise a driver could brute-force
    /// their way to a "verified" pickup that never happened.
    /// </summary>
    public int PinAttemptCount { get; set; }

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

    // ─── Card charge state (all null on a cash trip) ──────────────────────────

    /// <summary>
    /// The payment provider's intent for this trip's fare. Null until a charge
    /// starts. Once set it is never replaced for the same attempt — recovery
    /// re-reads THIS intent rather than creating a second one, because provider
    /// idempotency keys expire (24h at Stripe) and a fresh create after that
    /// would genuinely double-charge.
    /// </summary>
    public string? StripePaymentIntentId { get; set; }

    /// <summary>The saved card chosen at booking. Null for cash trips.</summary>
    public Guid? CustomerPaymentMethodId { get; set; }
    public CustomerPaymentMethod? CustomerPaymentMethod { get; set; }

    /// <summary>
    /// Claim marker for the charge pipeline, and <b>the whole answer to the
    /// dual-write hazard</b>.
    ///
    /// <para>
    /// Set by a single conditional UPDATE (see <c>TryStartChargeAsync</c>), so the
    /// attempt fired inline from <c>CompleteAsync</c> and the recovery sweeper can
    /// never both charge one trip — the loser simply walks away. A value older
    /// than the staleness window means the claiming process died mid-flight and
    /// the sweeper may re-claim it.
    /// </para>
    /// </summary>
    public DateTime? ChargeStartedAtUtc { get; set; }

    /// <summary>
    /// What was actually taken, in integer pence. Authoritative for
    /// reconciliation: the decimal-pounds columns above are a display
    /// representation that happens to be persisted, and this is the number that
    /// has to agree with the provider penny for penny.
    /// </summary>
    public int? AmountChargedPence { get; set; }

    /// <summary>Provider decline code, e.g. "insufficient_funds". Drives the retry decision.</summary>
    public string? PaymentFailureCode { get; set; }

    /// <summary>The customer-facing decline message. Safe to show; never a raw exception.</summary>
    public string? PaymentFailureMessage { get; set; }

    /// <summary>
    /// 0 for the first charge, then 1..N. Durable on purpose — it is what makes a
    /// retry's idempotency key stable across a crash or a double-tap.
    /// </summary>
    public int PaymentAttemptCount { get; set; }

    /// <summary>When the sweeper should next retry. Null = no automatic retry scheduled.</summary>
    public DateTime? NextPaymentRetryAtUtc { get; set; }

    /// <summary>
    /// Timestamp of the most recent provider event applied to this trip.
    /// Providers do not guarantee webhook ordering, so an event older than this
    /// is dropped — otherwise a late "processing" could un-settle a paid trip.
    /// </summary>
    public DateTime? LastPaymentEventAtUtc { get; set; }

    /// <summary>
    /// An admin forgave this fare. Clears the debt without inventing a ledger:
    /// the outstanding-balance query simply excludes waived trips.
    /// </summary>
    public DateTime? PaymentWaivedAtUtc { get; set; }
    public Guid? PaymentWaivedByAdminId { get; set; }

    /// <summary>RESERVED for a pre-auth hold. Never written — see PaymentStatus.Authorized.</summary>
    public DateTime? AuthorizedAtUtc { get; set; }

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
