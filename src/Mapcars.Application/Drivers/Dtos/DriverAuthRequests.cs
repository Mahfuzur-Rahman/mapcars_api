namespace Mapcars.Application.Drivers.Dtos;

// Request shapes for the driver auth endpoints. Kept in the Application layer
// (not the controller) so validators can target them and the API contract
// lives in one predictable place.

public record DriverPhoneRequest(string Phone);
public record DriverVerifyOtpRequest(string Phone, string Code);
public record DriverEmailSignUpRequest(string Email, string Password, string FullName);
public record DriverVerifyEmailRequest(string Email, string Code);
public record DriverEmailLoginRequest(string Email, string Password);
public record DriverGoogleAuthRequest(string IdToken);
