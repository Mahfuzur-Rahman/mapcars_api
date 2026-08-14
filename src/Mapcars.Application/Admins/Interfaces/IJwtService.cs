using Mapcars.Domain.Entities;

namespace Mapcars.Application.Admins.Interfaces;

public interface IJwtService
{
    string GenerateToken(Admin admin);
    /// <summary>Generates a JWT for a rider or driver.</summary>
    string GenerateUserToken(Guid userId, string? identifier, string userType);

    /// <summary>Access-token lifetime. Short on purpose — a JWT cannot be revoked
    /// once issued, so its life is the window an leaked one stays useful.</summary>
    int ExpiryMinutes { get; }

    /// <summary>
    /// Refresh-token lifetime, in days. Long on purpose — this is the credential
    /// that keeps a user signed in, and it *can* be revoked. Lives here rather
    /// than in the Application layer because reading configuration is
    /// Infrastructure's job.
    /// </summary>
    int RefreshTokenDays { get; }
}
