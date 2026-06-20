using Mapcars.Application.Admins.Dtos;

namespace Mapcars.Application.Admins.Interfaces;

public interface IAdminAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AdminResponse> RegisterAsync(CreateAdminRequest request, Guid createdBy, CancellationToken ct = default);
    Task<LoginResponse> SetupSuperAdminAsync(CreateAdminRequest request, CancellationToken ct = default);
    Task<LoginResponse> GetCurrentAdminAsync(Guid adminId, CancellationToken ct = default);
}
