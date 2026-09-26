namespace Mapcars.Application.Common.Dtos;

/// <summary>
/// Sign in with Apple, for either app.
/// </summary>
/// <param name="IdToken">The identity token from the Apple SDK.</param>
/// <param name="Nonce">The RAW nonce the app generated; the app passed its SHA-256 to Apple.</param>
/// <param name="FullName">
/// Apple hands the user's name to the app on the first authorisation only, and
/// never puts it in the token — so the app forwards it, and it is used only when
/// an account is being created.
/// </param>
public record AppleAuthRequest(string IdToken, string Nonce, string? FullName = null, bool SignUp = false);
