namespace Mapcars.Application.Auth.Dtos;

/// <summary>
/// <paramref name="LoginAs"/> ("rider" | "driver") is only needed on a second
/// call, after the first came back with <see cref="UnifiedLoginResponse.RequiresChoice"/>
/// set — the same email+password matched more than one account type.
/// </summary>
public record UnifiedLoginRequest(string Email, string Password, string? LoginAs = null);
