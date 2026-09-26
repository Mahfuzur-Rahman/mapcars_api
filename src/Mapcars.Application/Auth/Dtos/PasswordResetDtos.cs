namespace Mapcars.Application.Auth.Dtos;

/// <summary>
/// Ask for a password-reset code. <c>UserType</c> is "customer" or "driver" — one
/// email can hold both accounts, and a reset is for exactly one of them.
/// </summary>
public record ForgotPasswordRequest(string Email, string UserType);

/// <summary>Set a new password with the emailed 6-digit code.</summary>
public record ResetPasswordRequest(string Email, string UserType, string Code, string NewPassword);
