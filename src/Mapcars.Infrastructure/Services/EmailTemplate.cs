using Mapcars.Application.Common;
using B = Mapcars.Application.Common.EmailBrand;

namespace Mapcars.Infrastructure.Services;

/// <summary>
/// The branded layout every outgoing email is wrapped in: a cyan→green brand
/// stripe, the logo, the content card and a footer. Table-based with inline
/// styles on purpose — that is the subset Gmail, Outlook and Apple Mail agree on.
/// Content blocks come from <see cref="EmailHtml"/>.
/// </summary>
internal static class EmailTemplate
{
    /// <summary>
    /// <paramref name="content"/> is HTML, inserted as-is — callers encode what
    /// they interpolate into it (<see cref="EmailHtml"/>). <paramref name="title"/>
    /// is the subject, a plain-text header, so it is encoded here where it lands in
    /// the HTML: a contact-form subject carries the sender's name.
    /// </summary>
    internal static string Wrap(string title, string content) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <meta name="color-scheme" content="light">
          <meta name="supported-color-schemes" content="light">
          <title>{EmailHtml.Encode(title)}</title>
        </head>
        <body style="margin:0;padding:0;background:{B.Page};font-family:{B.FontStack};-webkit-font-smoothing:antialiased">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" bgcolor="{B.Page}" style="background:{B.Page}">
            <tr><td align="center" style="padding:32px 12px">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 1px 3px rgba(15,23,42,0.08)">
                <tr>
                  <td height="6" bgcolor="{B.Brand}" style="height:6px;line-height:6px;font-size:0;background:{B.Brand};background-image:linear-gradient(90deg,{B.Brand},{B.Accent})">&nbsp;</td>
                </tr>
                <tr>
                  <td style="padding:24px 32px 0">
                    <a href="{B.SiteUrl}" style="text-decoration:none">
                      <img src="{B.LogoUrl}" width="180" alt="MapCars" style="display:block;width:180px;max-width:180px;height:auto;border:0;color:{B.Ink};font-size:22px;font-weight:800">
                    </a>
                  </td>
                </tr>
                <tr>
                  <td style="padding:16px 36px 36px">
                    <h1 style="margin:0 0 18px;color:{B.Ink};font-size:24px;line-height:1.3;font-weight:800">{EmailHtml.Encode(title)}</h1>
                    <div style="color:{B.Body};font-size:15px;line-height:1.65">{content}</div>
                  </td>
                </tr>
                <tr>
                  <td bgcolor="{B.Soft}" style="background:{B.Soft};border-top:1px solid {B.Line};padding:22px 36px">
                    <p style="margin:0 0 6px;color:{B.Body};font-size:13px;line-height:1.6">
                      Questions? Contact us at
                      <a href="mailto:{B.SupportEmail}" style="color:{B.BrandDark};font-weight:600;text-decoration:none">{B.SupportEmail}</a>
                    </p>
                    <p style="margin:0;color:{B.Muted};font-size:12px;line-height:1.6">
                      {EmailHtml.Encode(B.CompanyName)} · {EmailHtml.Encode(B.CompanyAddress)}<br>
                      This is an automated message — please don't reply to it.
                    </p>
                  </td>
                </tr>
              </table>
              <p style="margin:18px 0 0;color:{B.Muted};font-size:12px">
                © {DateTime.UtcNow.Year} {EmailHtml.Encode(B.CompanyName)} ·
                <a href="{B.SiteUrl}" style="color:{B.Muted};text-decoration:underline">mapcars.uk</a>
              </p>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}
