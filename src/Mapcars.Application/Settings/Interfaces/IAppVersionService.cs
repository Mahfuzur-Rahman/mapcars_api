using Mapcars.Application.Settings.Dtos;

namespace Mapcars.Application.Settings.Interfaces;

public interface IAppVersionService
{
    /// <summary>
    /// The newest build for one app + platform, or null when <paramref name="app"/>
    /// or <paramref name="platform"/> is not a known name.
    /// </summary>
    Task<AppVersionEntryDto?> GetAsync(string app, string platform, CancellationToken ct = default);

    Task<AppVersionsDocument> GetForAdminAsync(CancellationToken ct = default);

    Task<AppVersionsDocument> UpdateAsync(AppVersionsDocument request, Guid adminId, CancellationToken ct = default);
}
