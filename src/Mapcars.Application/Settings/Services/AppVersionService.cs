using Mapcars.Application.Settings.Dtos;
using Mapcars.Application.Settings.Interfaces;
using Mapcars.Application.Settings.Models;

namespace Mapcars.Application.Settings.Services;

/// <summary>Reads and publishes the newest-build numbers the apps nudge towards.</summary>
public class AppVersionService(ISettingsStore settings) : IAppVersionService
{
    public async Task<AppVersionEntryDto?> GetAsync(string app, string platform, CancellationToken ct = default)
    {
        var doc = await settings.GetAsync<AppVersionSettings>(SettingKeys.AppVersions, ct);
        var entry = (app.ToLowerInvariant(), platform.ToLowerInvariant()) switch
        {
            (AppVersionNames.Customer, AppVersionNames.Android) => doc.CustomerAndroid,
            (AppVersionNames.Customer, AppVersionNames.Ios) => doc.CustomerIos,
            (AppVersionNames.Driver, AppVersionNames.Android) => doc.DriverAndroid,
            (AppVersionNames.Driver, AppVersionNames.Ios) => doc.DriverIos,
            _ => null,
        };
        return entry is null ? null : ToDto(entry);
    }

    public async Task<AppVersionsDocument> GetForAdminAsync(CancellationToken ct = default)
        => ToDocument(await settings.GetAsync<AppVersionSettings>(SettingKeys.AppVersions, ct));

    public async Task<AppVersionsDocument> UpdateAsync(
        AppVersionsDocument request, Guid adminId, CancellationToken ct = default)
    {
        var next = new AppVersionSettings
        {
            CustomerAndroid = ToEntry(request.CustomerAndroid),
            CustomerIos = ToEntry(request.CustomerIos),
            DriverAndroid = ToEntry(request.DriverAndroid),
            DriverIos = ToEntry(request.DriverIos),
        };
        return ToDocument(await settings.SetAsync(SettingKeys.AppVersions, next, adminId, ct));
    }

    private static AppVersionEntry ToEntry(AppVersionEntryDto d) => new()
    {
        Latest = d.Latest.Trim(),
        StoreUrl = d.StoreUrl.Trim(),
        Message = d.Message.Trim(),
    };

    private static AppVersionEntryDto ToDto(AppVersionEntry e) => new(e.Latest, e.StoreUrl, e.Message);

    private static AppVersionsDocument ToDocument(AppVersionSettings s) => new(
        ToDto(s.CustomerAndroid), ToDto(s.CustomerIos), ToDto(s.DriverAndroid), ToDto(s.DriverIos));
}
