namespace Mapcars.Application.Riders.Dtos;

// Request shapes for the rider auth endpoints. Kept in the Application layer
// (not the controller) so validators can target them and the API contract
// lives in one predictable place.

public record PhoneRequest(string Phone);
public record VerifyOtpRequest(string Phone, string Code);
public record EmailSignUpRequest(string Email, string Password, string FullName);
public record VerifyEmailRequest(string Email, string Code);
public record EmailLoginRequest(string Email, string Password);
public record GoogleAuthRequest(string IdToken);
public record UpdateProfileRequest(string FullName, string? Email = null);
