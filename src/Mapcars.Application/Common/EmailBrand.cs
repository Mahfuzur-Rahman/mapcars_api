namespace Mapcars.Application.Common;

/// <summary>
/// The brand as email sees it — one place for the palette, the logo and the
/// footer facts, shared by the layout (<c>EmailTemplate</c>) and the content
/// blocks (<see cref="EmailHtml"/>). Colours mirror the web tokens in
/// <c>web/src/app/globals.css</c>; email can't read CSS variables, so they are
/// repeated here.
/// </summary>
public static class EmailBrand
{
    public const string SiteUrl = "https://mapcars.uk";

    /// <summary>
    /// Served by the web app (<c>web/public/assets/images</c>). Email clients
    /// need an absolute URL to a PNG — Gmail and Outlook show no SVG — so moving
    /// or renaming this file on the web side blanks the header of every email.
    /// </summary>
    public const string LogoUrl = SiteUrl + "/assets/images/logo-trans-color-hd.png";

    public const string SupportEmail = "support@mapcars.uk";
    public const string CompanyName = "MapCars UK Ltd";
    public const string CompanyAddress = "Bournemouth & Poole, Dorset, United Kingdom";

    public const string Brand = "#00b9d9";     // --brand (cyan)
    public const string BrandDark = "#0099b8"; // --brand-dark
    public const string Accent = "#4dc842";    // --accent (green)
    public const string Ink = "#0f172a";       // --foreground
    public const string Body = "#334155";
    public const string Muted = "#64748b";
    public const string Line = "#e2e8f0";
    public const string Soft = "#f8fafc";
    public const string Page = "#eef2f6";

    public const string FontStack =
        "'Inter','Segoe UI',Roboto,'Helvetica Neue',Helvetica,Arial,sans-serif";
}
