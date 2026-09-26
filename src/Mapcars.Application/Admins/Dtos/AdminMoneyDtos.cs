namespace Mapcars.Application.Admins.Dtos;

/// <summary>
/// One money movement on the admin Transactions page: a completed trip, with the
/// fare split into the platform's commission and the driver's share.
///
/// <para>
/// Unlike every driver-facing view, this carries the customer's REAL payment
/// state — including declines and the provider's failure code. That is what the
/// page is for: it is where a failed charge gets noticed and chased.
/// </para>
/// </summary>
public record AdminTransactionItem(
    Guid TripId,
    DateTime CompletedAtUtc,
    string? CustomerName,
    string? DriverName,
    string? Tier,
    string PaymentMethod,
    string PaymentStatus,
    decimal FareAmount,
    decimal TipAmount,
    decimal PlatformFeeAmount,
    decimal DriverEarnings,
    DateTime? PaidAtUtc,
    string? PaymentFailureCode,
    string? StripePaymentIntentId,
    bool IsWaived);

/// <summary>Totals over the whole filtered range, not just the returned page.</summary>
/// <param name="Outstanding">
/// Card fares (plus tips) that failed or await authentication and were not
/// waived — money customers owe the platform.
/// </param>
public record AdminTransactionTotals(
    int Count,
    decimal GrossFares,
    decimal Tips,
    decimal Commission,
    decimal DriverEarnings,
    decimal CashCollected,
    decimal CardCollected,
    decimal Outstanding);

public record AdminTransactionsPage(
    IReadOnlyList<AdminTransactionItem> Items,
    AdminTransactionTotals Totals,
    int Skip,
    int Take);

/// <summary>A payout sent (or being sent) to a driver's Stripe connected account.</summary>
public record AdminPayoutItem(
    Guid Id,
    Guid DriverId,
    string? DriverName,
    decimal Amount,
    string Currency,
    string Status,
    string StripePayoutId,
    DateTime CreatedAtUtc,
    DateTime? ArrivedAtUtc);

/// <param name="InFlight">Pending or in transit.</param>
public record AdminPayoutTotals(int Count, decimal Paid, decimal InFlight, decimal Failed);

public record AdminPayoutsPage(
    IReadOnlyList<AdminPayoutItem> Items,
    AdminPayoutTotals Totals,
    int Skip,
    int Take);
