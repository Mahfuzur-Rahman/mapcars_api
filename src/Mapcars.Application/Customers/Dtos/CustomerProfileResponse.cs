namespace Mapcars.Application.Customers.Dtos;

/// <summary>
/// Full customer profile — richer than the shared AuthResponse (which is used
/// by both customers and drivers across login/signup), so customer-only fields
/// like emergency contact/marketing consent/accessibility needs live here
/// instead. Mirrors Drivers/Dtos/DriverProfileResponse.cs.
/// </summary>
public class CustomerProfileResponse
{
    public Guid CustomerId { get; set; }

    /// <summary>DEPRECATED alias for <c>CustomerId</c>, kept for one release so a client
    /// build that predates the rename keeps working. Delete once web and both apps
    /// have shipped. See TODO_PAYMENTS.md phase 1b.</summary>
    public Guid RiderId => CustomerId;

    public string? FullName { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// Trip receipts are only emailed to a verified address. False for an email a
    /// phone sign-up added on the profile until they confirm it
    /// (<c>me/email/send-code</c> → <c>me/email/verify</c>).
    /// </summary>
    public bool IsEmailVerified { get; set; }
    public string? Phone { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public bool MarketingConsent { get; set; }
    public string? AccessibilityNeeds { get; set; }
    public bool HasProfilePicture { get; set; }
    public bool IsProfileComplete { get; set; }

    public decimal? AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int CancellationCount { get; set; }
    public int NoShowCount { get; set; }
}
