using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// A customer's saved card, as an opaque provider reference plus the
/// display-safe metadata the provider hands back.
///
/// <para>
/// <b>Nothing here is card data.</b> No PAN, no CVC, no chargeable expiry — only
/// the token and enough to render "Visa •••• 4242, expires 04/28". That is what
/// keeps this application in the lightest PCI tier, and it is a hard rule: if a
/// future column here would hold a card number, the design is wrong, not the
/// column.
/// </para>
/// </summary>
public class CustomerPaymentMethod : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>The provider's payment-method token — the only handle we hold.</summary>
    public required string StripePaymentMethodId { get; set; }

    /// <summary>
    /// "card" | "apple_pay" | "google_pay". Wallets arrive as cards carrying a
    /// wallet sub-type; flattened here because the distinction only matters for
    /// display and for explaining a wallet-specific failure.
    /// </summary>
    public string Type { get; set; } = "card";

    public string? Brand { get; set; }
    public string? Last4 { get; set; }
    public int? ExpMonth { get; set; }
    public int? ExpYear { get; set; }

    /// <summary>"credit" | "debit" | "prepaid".</summary>
    public string? FundingType { get; set; }

    /// <summary>Issuing country, e.g. "GB".</summary>
    public string? Country { get; set; }

    /// <summary>
    /// At most one per customer. Enforced by a partial unique index rather than
    /// application code — two defaults would make "which card do we charge?" a
    /// coin toss at drop-off.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Soft-deactivated on removal, or after a non-retryable decline (expired,
    /// lost, stolen). Kept rather than deleted so a historical trip can still
    /// name the card it was charged on.
    /// </summary>
    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAtUtc { get; set; }
    public string? DeactivationReason { get; set; }

    /// <summary>
    /// The setup intent that established the off-session mandate, and when the
    /// customer accepted it. The audit trail for "was this card authenticated
    /// when it was saved?" — which is the question that decides whether a later
    /// off-session charge is allowed to skip authentication.
    /// </summary>
    public string? StripeSetupIntentId { get; set; }
    public DateTime? MandateAcceptedAtUtc { get; set; }

    /// <summary>Cards a customer can actually be charged on.</summary>
    public bool IsUsable => IsActive && !IsExpired(DateTime.UtcNow);

    /// <summary>
    /// A card expires at the END of its expiry month, not the start.
    /// Unknown expiry counts as not expired — the provider is the authority, and
    /// hiding a usable card is worse than letting it decline.
    /// </summary>
    public bool IsExpired(DateTime nowUtc)
    {
        if (ExpYear is not int year || ExpMonth is not int month) return false;
        if (month is < 1 or > 12) return false;
        var firstOfNextMonth = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        return nowUtc >= firstOfNextMonth;
    }
}
