namespace Mapcars.Application.Settings.Models;

/// <summary>
/// The newest published build of each mobile app, per platform. Stored as one
/// JSON document under <see cref="SettingKeys.AppVersions"/>, edited from the
/// admin portal, live immediately with no deploy.
///
/// <para>
/// <b>Soft nudge only.</b> The apps compare their own version with
/// <c>Latest</c> and offer a dismissible "update available" prompt. There is
/// deliberately no minimum version that blocks old builds. If one is wanted
/// later, add a <c>Minimum</c> field here — a document written before it
/// existed deserialises with the default, so no migration is needed.
/// </para>
///
/// <para>
/// Defaults are empty: an empty <c>Latest</c> means "nothing to announce", so a
/// fresh install of the API never nags anyone.
/// </para>
/// </summary>
public class AppVersionSettings
{
    public AppVersionEntry CustomerAndroid { get; set; } = new();
    public AppVersionEntry CustomerIos { get; set; } = new();
    public AppVersionEntry DriverAndroid { get; set; } = new();
    public AppVersionEntry DriverIos { get; set; } = new();
}

public class AppVersionEntry
{
    /// <summary>Newest version name, e.g. "1.2.0". Empty = no update to announce.</summary>
    public string Latest { get; set; } = "";

    /// <summary>Where the "Update" button goes: the Play / App Store / TestFlight link.</summary>
    public string StoreUrl { get; set; } = "";

    /// <summary>Optional one-liner shown in the prompt, e.g. "Faster booking and bug fixes".</summary>
    public string Message { get; set; } = "";
}

/// <summary>The wire spelling of the app and platform names the clients send.</summary>
public static class AppVersionNames
{
    public const string Customer = "customer";
    public const string Driver = "driver";
    public const string Android = "android";
    public const string Ios = "ios";
}
