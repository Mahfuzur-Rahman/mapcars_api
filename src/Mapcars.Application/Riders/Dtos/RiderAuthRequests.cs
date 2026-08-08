namespace Mapcars.Application.Riders.Dtos;

// Request shapes for the rider auth endpoints. Kept in the Application layer
// (not the controller) so validators can target them and the API contract
// lives in one predictable place.

public record PhoneRequest(string Phone);
public record VerifyOtpRequest(string Phone, string Code);
public record EmailSignUpRequest(string Email, string Password, string FullName);
public record ResendEmailRequest(string Email);
public record VerifyEmailRequest(string Email, string Code);
public record EmailLoginRequest(string Email, string Password);
/// <summary>
/// Google sign-in. <paramref name="SignUp"/> says which screen the rider came
/// from: <c>true</c> from "Sign up with Google" (an account may be created),
/// <c>false</c> (the default) from "Continue with Google" on the sign-in page —
/// where an unknown Google account is told to sign up rather than silently
/// becoming a new account.
/// </summary>
public record GoogleAuthRequest(string IdToken, bool SignUp = false);
public record UpdateProfileRequest(
    string FullName,
    string? Email = null,
    string? EmergencyContactName = null,
    string? EmergencyContactPhone = null,
    bool? MarketingConsent = null,
    string? AccessibilityNeeds = null);
