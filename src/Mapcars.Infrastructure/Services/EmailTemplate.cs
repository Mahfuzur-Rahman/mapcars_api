namespace Mapcars.Infrastructure.Services;

internal static class EmailTemplate
{
    internal static string Wrap(string title, string content) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;padding:0;background:#f5f5f5;font-family:Arial,sans-serif">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr><td align="center" style="padding:40px 16px">
              <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden">
                <tr>
                  <td style="background:#0f0f0f;padding:24px 32px">
                    <span style="color:#ffffff;font-size:22px;font-weight:700;letter-spacing:1px">MAP CARS</span>
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px">
                    <h2 style="margin:0 0 16px;color:#1a1a1a;font-size:20px">{title}</h2>
                    <div style="color:#444;font-size:15px;line-height:1.6">{content}</div>
                  </td>
                </tr>
                <tr>
                  <td style="padding:16px 32px;border-top:1px solid #eee">
                    <p style="margin:0;color:#888;font-size:12px">
                      MAP CARS · UK Ride-Hailing · Do not reply to this email.
                    </p>
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}
