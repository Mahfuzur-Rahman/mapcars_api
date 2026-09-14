using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

public class Customer : BaseEntity
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PasswordHash { get; set; }
    public string? GoogleSub { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool IsActive { get; set; } = true;

    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public bool MarketingConsent { get; set; }
    public string? AccessibilityNeeds { get; set; }
    public string? ProfilePictureKey { get; set; }
    public string? ProfilePictureContentType { get; set; }

    public int CancellationCount { get; set; }
    public int NoShowCount { get; set; }
    public decimal? AverageRating { get; set; }
    public int RatingCount { get; set; }

    /// <summary>
    /// The payment provider's customer record, created lazily on the first card
    /// save and never before. A customer who only ever pays cash has none, and
    /// that is the point — no account is created at the provider for someone who
    /// has not asked to save a card.
    /// </summary>
    public string? StripeCustomerId { get; set; }

    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
    public ICollection<CustomerPaymentMethod> PaymentMethods { get; set; } = new List<CustomerPaymentMethod>();
    public ICollection<SavedPlace> SavedPlaces { get; set; } = new List<SavedPlace>();

    public bool IsProfileComplete => !string.IsNullOrEmpty(FullName);
}
