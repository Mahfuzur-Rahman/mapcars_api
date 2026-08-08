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
/// <summary>
/// Google sign-in. <paramref name="SignUp"/> says which screen the driver came
/// from: <c>true</c> from "Sign up with Google" (an account may be created),
/// <c>false</c> (the default) from the sign-in screen — where an unknown Google
/// account is told to sign up rather than silently becoming a new account.
/// </summary>
public record DriverGoogleAuthRequest(string IdToken, bool SignUp = false);
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
