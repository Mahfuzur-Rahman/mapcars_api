using Mapcars.Application.Admins.Interfaces;

namespace Mapcars.Application.Admins.Services;

/// <summary>
/// Loads the admin's access once and answers from <see cref="AdminMenuAccess"/>.
/// Registered scoped, so the cached profile lives exactly as long as the request:
/// at most one lookup per request, and a menu change made by a SuperAdmin applies
/// from the admin's very next call — nothing is cached across requests.
/// </summary>
public class AdminMenuAuthorizer(IAdminMenuAccessRepository access) : IAdminMenuAuthorizer
{
    private Guid? _loadedFor;
    private AdminAccessProfile? _profile;

    public async Task<bool> CanUseAnyAsync(Guid adminId, IReadOnlyCollection<string> menuPaths, CancellationToken ct = default)
    {
        if (_loadedFor != adminId)
        {
            _profile = await access.GetAccessProfileAsync(adminId, ct);
            _loadedFor = adminId;
        }

        return AdminMenuAccess.CanUseAny(_profile, menuPaths);
    }
}
