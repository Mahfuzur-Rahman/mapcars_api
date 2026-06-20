namespace Mapcars.Application.Common.Interfaces;

public class GoogleUserInfo
{
    public string Sub { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
}

public interface IGoogleAuthService
{
    Task<GoogleUserInfo?> VerifyIdTokenAsync(string idToken, CancellationToken ct = default);
}
