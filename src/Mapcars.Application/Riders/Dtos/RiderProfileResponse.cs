namespace Mapcars.Application.Riders.Dtos;

/// <summary>
/// Full rider profile — richer than the shared AuthResponse (which is used
/// by both riders and drivers across login/signup), so rider-only fields
/// like emergency contact/marketing consent/accessibility needs live here
/// instead. Mirrors Drivers/Dtos/DriverProfileResponse.cs.
/// </summary>
public class RiderProfileResponse
{
    public Guid RiderId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public bool MarketingConsent { get; set; }
    public string? AccessibilityNeeds { get; set; }
    public bool IsProfileComplete { get; set; }

    public decimal? AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int CancellationCount { get; set; }
    public int NoShowCount { get; set; }
}
