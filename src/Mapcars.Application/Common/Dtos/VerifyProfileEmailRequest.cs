namespace Mapcars.Application.Common.Dtos;

/// <summary>
/// Confirms the email on a signed-in account's profile with the code sent to it.
/// No email field on purpose: the code is checked against the address already on
/// the profile, so it can't be redirected to a different one.
/// </summary>
public record VerifyProfileEmailRequest(string Code);
