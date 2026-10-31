namespace Mapcars.Application.Settings.Dtos;

/// <summary>One app + platform's newest build. Public: read by the apps at launch.</summary>
public record AppVersionEntryDto(string Latest, string StoreUrl, string Message);

/// <summary>
/// All four entries. Used both as the admin portal's read model and as its
/// publish request, so the form round-trips what it loaded.
/// </summary>
public record AppVersionsDocument(
    AppVersionEntryDto CustomerAndroid,
    AppVersionEntryDto CustomerIos,
    AppVersionEntryDto DriverAndroid,
    AppVersionEntryDto DriverIos);
