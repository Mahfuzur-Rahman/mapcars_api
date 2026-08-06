using Mapcars.Application.Auth.Dtos;

namespace Mapcars.Application.Auth.Interfaces;

public interface IUnifiedAuthService
{
    /// <summary>
    /// Looks up the email across Admin, Rider, and Driver accounts and logs
    /// into whichever one the password actually matches.
    /// </summary>
    Task<UnifiedLoginResponse> LoginAsync(UnifiedLoginRequest request, CancellationToken ct = default);
}
