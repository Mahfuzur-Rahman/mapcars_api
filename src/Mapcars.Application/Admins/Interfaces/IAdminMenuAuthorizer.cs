namespace Mapcars.Application.Admins.Interfaces;

/// <summary>
/// Server-side enforcement of admin menu permissions — the decision behind the
/// API's <c>[RequiresAdminMenu]</c>. Scoped: one instance per request.
/// </summary>
public interface IAdminMenuAuthorizer
{
    /// <summary>
    /// True when the admin is active and is a SuperAdmin or holds at least one of
    /// the menus at <paramref name="menuPaths"/>. See <see cref="AdminMenuAccess.CanUseAny"/>.
    /// </summary>
    Task<bool> CanUseAnyAsync(Guid adminId, IReadOnlyCollection<string> menuPaths, CancellationToken ct = default);
}
