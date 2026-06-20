using Mapcars.Domain.Entities;

namespace Mapcars.Application.Admins.Interfaces;

public interface IJwtService
{
    string GenerateToken(Admin admin);
    /// <summary>Generates a JWT for a rider or driver.</summary>
    string GenerateUserToken(Guid userId, string? identifier, string userType);
    int ExpiryMinutes { get; }
}
