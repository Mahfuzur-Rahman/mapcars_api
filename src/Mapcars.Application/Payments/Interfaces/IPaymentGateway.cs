namespace Mapcars.Application.Payments.Interfaces;

/// <summary>What a charge attempt came back as, normalised away from any one provider.</summary>
public enum PaymentOutcome
{
    /// <summary>Money taken. Terminal.</summary>
    Succeeded,

    /// <summary>The customer must authenticate (SCA) before this can settle.</summary>
    RequiresAction,

    /// <summary>Accepted but not yet settled. Resolves by webhook, never synchronously.</summary>
    Processing,

    /// <summary>Declined. <see cref="PaymentResult.DeclineCode"/> says whether retrying is worth it.</summary>
    Declined,

    /// <summary>The provider cancelled the intent.</summary>
    Canceled,
}

/// <summary>
/// A charge to attempt.
///
/// <para>
/// <see cref="AmountPence"/> is integer minor units — the amount is never a
/// decimal on this boundary. <see cref="IdempotencyKey"/> is derived from the
/// trip, never generated fresh, which is the single thing standing between a
/// retry and a double charge.
/// </para>
/// </summary>
public record ChargeRequest(
    int AmountPence,
    string Currency,
    string ProviderCustomerId,
    string ProviderPaymentMethodId,
    string Description,
    string IdempotencyKey,
    IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>
    /// True for a merchant-initiated charge with nobody present — the drop-off
    /// case. False when the customer is in the app and can be shown an
    /// authentication challenge, which is the only way to clear a
    /// <see cref="PaymentOutcome.RequiresAction"/>.
    /// </summary>
    public bool OffSession { get; init; } = true;

    /// <summary>
    /// "automatic" today. This single field is the whole switch for
    /// authorise-at-booking: flipping it to manual plus a capture at completion
    /// is the entire change, because the claim, the idempotency key, the recovery
    /// sweeper and the webhook handling are already shaped for it. Not built.
    /// </summary>
    public string CaptureMethod { get; init; } = "automatic";
}

/// <summary>The normalised result of a charge attempt.</summary>
public record PaymentResult(
    PaymentOutcome Outcome,
    string ProviderPaymentIntentId,
    int? AmountReceivedPence = null,
    string? DeclineCode = null,
    string? CustomerMessage = null,
    /// <summary>Only present on <see cref="PaymentOutcome.RequiresAction"/>. Returned to the
    /// app once and never persisted or logged.</summary>
    string? ClientSecret = null,
    /// <summary>When the provider says this happened. Used to drop out-of-order webhooks.</summary>
    DateTime? OccurredAtUtc = null);

/// <summary>A saved card, as the provider describes it.</summary>
public record ProviderPaymentMethod(
    string Id,
    string Type,
    string? Brand,
    string? Last4,
    int? ExpMonth,
    int? ExpYear,
    string? FundingType,
    string? Country);

/// <summary>Everything the app needs to open the provider's card-entry sheet.</summary>
public record SetupIntentTicket(
    string ClientSecret,
    string ProviderCustomerId,
    string? EphemeralKeySecret);

/// <summary>
/// A provider error, classified.
///
/// <para>
/// <see cref="IsCardError"/> separates "the card said no" — an expected outcome
/// with a decline code, which the caller turns into customer debt — from
/// everything else (network, auth, provider outage), where we genuinely do not
/// know whether money moved and must NOT mark the charge failed.
/// </para>
/// </summary>
public class PaymentGatewayException : Exception
{
    public PaymentGatewayException(
        string message,
        bool isCardError = false,
        string? declineCode = null,
        string? customerMessage = null,
        string? providerPaymentIntentId = null,
        Exception? inner = null) : base(message, inner)
    {
        IsCardError = isCardError;
        DeclineCode = declineCode;
        CustomerMessage = customerMessage;
        ProviderPaymentIntentId = providerPaymentIntentId;
    }

    public bool IsCardError { get; }
    public string? DeclineCode { get; }

    /// <summary>Safe to show a customer. Never a raw provider exception string.</summary>
    public string? CustomerMessage { get; }

    /// <summary>Set when the failure still produced an intent worth recovering.</summary>
    public string? ProviderPaymentIntentId { get; }
}

/// <summary>
/// The entire payment-provider surface, in one interface.
///
/// <para>
/// Deliberately wide. Keeping every provider call behind this seam is what lets
/// the charge pipeline — the claim, the retry policy, the recovery sweeper, the
/// out-of-order webhook guard — be built and tested with no account, no keys and
/// no network. A fake implementation can reach branches that are otherwise
/// nearly impossible to produce on demand: a network timeout followed by the
/// same idempotency key returning the same intent, or a webhook arriving before
/// the synchronous result.
/// </para>
///
/// <para>
/// It also keeps the provider SDK out of the Application layer, which the
/// architecture requires. Resist adding "just one" direct SDK call to a service.
/// </para>
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Creates the provider customer if this one does not have it yet.</summary>
    Task<string> EnsureCustomerAsync(
        Guid customerId, string? existingProviderCustomerId, string? email, string? name,
        CancellationToken ct = default);

    /// <summary>
    /// Starts card entry. The intent must be created for OFF-SESSION future use:
    /// that is what establishes the mandate a later unattended charge relies on,
    /// and getting it wrong makes every drop-off charge fail authentication.
    /// </summary>
    Task<SetupIntentTicket> CreateSetupIntentAsync(
        string providerCustomerId, CancellationToken ct = default);

    /// <summary>Reads back what was actually saved, after the app finishes card entry.</summary>
    Task<ProviderPaymentMethod?> GetSetupIntentPaymentMethodAsync(
        string setupIntentId, CancellationToken ct = default);

    Task<ProviderPaymentMethod?> GetPaymentMethodAsync(string paymentMethodId, CancellationToken ct = default);

    /// <summary>Detaches a saved card at the provider, so removal is not local-only.</summary>
    Task DetachPaymentMethodAsync(string paymentMethodId, CancellationToken ct = default);

    /// <summary>
    /// Creates and confirms a charge. Must send BOTH the customer and the payment
    /// method: omitting the customer loses the link to the mandate and the charge
    /// is challenged even though it should be exempt.
    /// </summary>
    Task<PaymentResult> CreateAndConfirmAsync(ChargeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Reads an existing intent. This is how recovery works — never by creating a
    /// second intent, because idempotency keys expire and a fresh create after
    /// that would genuinely charge twice.
    /// </summary>
    Task<PaymentResult> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct = default);

    /// <summary>Confirms on-session, so an authentication challenge can be presented.</summary>
    Task<PaymentResult> ConfirmAsync(string paymentIntentId, CancellationToken ct = default);

    Task<PaymentResult> CancelAsync(string paymentIntentId, CancellationToken ct = default);

    /// <summary>Full refund unless an amount is given.</summary>
    Task<PaymentResult> RefundAsync(
        string paymentIntentId, int? amountPence, string idempotencyKey, CancellationToken ct = default);
}
