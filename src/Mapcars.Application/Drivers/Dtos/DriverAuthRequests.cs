namespace Mapcars.Application.Drivers.Dtos;

// Request shapes for the driver auth endpoints. Kept in the Application layer
// (not the controller) so validators can target them and the API contract
// lives in one predictable place.

public record DriverPhoneRequest(string Phone);
public record DriverVerifyOtpRequest(string Phone, string Code);
public record DriverEmailSignUpRequest(string Email, string Password, string FullName);
public record DriverResendEmailRequest(string Email);
public record DriverVerifyEmailRequest(string Email, string Code);
public record DriverEmailLoginRequest(string Email, string Password);
public record DriverGoogleAuthRequest(string IdToken);
public record UpdateDriverProfileRequest(
    string FirstName,
    string? LastName,
    string? Email,
    DateOnly? DateOfBirth,
    string? Address,
    string NationalIdNumber,
    string? DrivingLicenceNumber = null,
    string? PassportNumber = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    bool? MarketingConsent = null);

/// <summary>Toggle the authenticated driver's online/offline availability.</summary>
public record UpdateDriverAvailabilityRequest(bool IsOnline);
