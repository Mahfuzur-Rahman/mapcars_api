namespace Mapcars.Application.Admins.Interfaces;

/// <summary>
/// The read behind the per-request admin menu check. Kept apart from
/// <see cref="IAdminRepository"/> because it runs on every gated admin call and
/// has one job: fetch the inputs to <see cref="AdminMenuAccess"/> in one go.
/// </summary>
public interface IAdminMenuAccessRepository
{
    /// <summary>
    /// The admin's account state plus both inputs to the menu rule, in a single
    /// database round trip. Null when no admin has this id.
    /// </summary>
    Task<AdminAccessProfile?> GetAccessProfileAsync(Guid adminId, CancellationToken ct = default);
}
