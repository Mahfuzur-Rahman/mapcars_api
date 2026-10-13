using System.Net;
using B = Mapcars.Application.Common.EmailBrand;

namespace Mapcars.Application.Common;

/// <summary>
/// Email bodies handed to <c>IEmailService</c> are HTML: the provider sends them
/// inside the branded template as-is, and the admin Email page renders the
/// stored copy. So every value a customer, driver, admin or outside sender can
/// influence — names, addresses, notes, free text — goes through
/// <see cref="Encode"/> before it is interpolated (a driver may name themselves
/// <c>&lt;img src=x onerror=…&gt;</c>). Static copy, numbers, enum names and
/// server-generated codes don't need it.
/// <para>
/// Subjects are not encoded here: they are plain-text headers, and the template
/// encodes its own copy where it shows one in the HTML.
/// </para>
/// <para>
/// The block builders below are the email equivalent of UI components: inline
/// styles and tables only, because that is all Gmail and Outlook reliably
/// render. Methods taking <c>html</c> insert it as-is (the caller encodes);
/// every other string parameter is plain text and is encoded here.
/// </para>
/// </summary>
public static class EmailHtml
{
    /// <summary>Safe in element content and in quoted attribute values (quotes are encoded too).</summary>
    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public static string Paragraph(string html) =>
        $"""<p style="margin:0 0 16px;color:{B.Body};font-size:15px;line-height:1.65">{html}</p>""";

    /// <summary>Small grey print — reassurance lines, "ignore this if…".</summary>
    public static string Muted(string html) =>
        $"""<p style="margin:16px 0 0;color:{B.Muted};font-size:13px;line-height:1.6">{html}</p>""";

    /// <summary>Small uppercase label above a group of blocks.</summary>
    public static string Heading(string text) =>
        $"""<p style="margin:28px 0 10px;color:{B.Muted};font-size:12px;font-weight:700;letter-spacing:1.2px;text-transform:uppercase">{Encode(text)}</p>""";

    /// <summary>A one-time code, large and spaced so it reads (and copies) at a glance.</summary>
    public static string Code(string code) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:8px 0 24px">
          <tr><td style="background:{B.Soft};border:1px solid {B.Line};border-radius:12px;padding:16px 28px;font-family:'SFMono-Regular',Consolas,'Courier New',monospace;font-size:32px;font-weight:700;letter-spacing:8px;color:{B.Ink}">{Encode(code)}</td></tr>
        </table>
        """;

    /// <summary>
    /// A filled call-to-action. The table + bgcolor pair is what survives Outlook,
    /// which ignores padding and background on a bare &lt;a&gt;.
    /// </summary>
    public static string Button(string text, string url) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:8px 0 24px">
          <tr><td bgcolor="{B.Brand}" style="border-radius:10px;background:{B.Brand}">
            <a href="{Encode(url)}" style="display:inline-block;padding:13px 26px;color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;border-radius:10px">{Encode(text)}</a>
          </td></tr>
        </table>
        """;

    /// <summary>The one number the email is about — a total, an earning — with a caption under it.</summary>
    public static string Amount(string label, string amount, string? caption = null) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:8px 0 8px">
          <tr><td style="background:{B.Soft};border:1px solid {B.Line};border-radius:14px;padding:22px 24px">
            <div style="color:{B.Muted};font-size:12px;font-weight:700;letter-spacing:1.2px;text-transform:uppercase">{Encode(label)}</div>
            <div style="margin-top:6px;color:{B.Ink};font-size:34px;font-weight:800;line-height:1.1">{Encode(amount)}</div>
            {(caption is null ? "" : $"""<div style="margin-top:8px;color:{B.Body};font-size:14px">{Encode(caption)}</div>""")}
          </td></tr>
        </table>
        """;

    /// <summary>Pickup → drop-off, with the brand's green and cyan as the two stops.</summary>
    public static string Route(string pickup, string dropoff) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border:1px solid {B.Line};border-radius:14px">
          {RouteStop(B.Accent, "Pickup", pickup, first: true)}
          {RouteStop(B.Brand, "Drop-off", dropoff, first: false)}
        </table>
        """;

    private static string RouteStop(string colour, string label, string address, bool first) => $"""
        <tr>
          <td width="34" valign="top" style="padding:{(first ? "18px" : "14px")} 0 {(first ? "14px" : "18px")} 18px">
            <div style="width:12px;height:12px;border-radius:6px;background:{colour};margin-top:4px"></div>
          </td>
          <td style="padding:{(first ? "16px" : "12px")} 18px {(first ? "12px" : "16px")} 4px;{(first ? $"border-bottom:1px solid {B.Line};" : "")}">
            <div style="color:{B.Muted};font-size:12px;font-weight:700;letter-spacing:1px;text-transform:uppercase">{Encode(label)}</div>
            <div style="margin-top:3px;color:{B.Ink};font-size:15px;line-height:1.45">{Encode(address)}</div>
          </td>
        </tr>
        """;

    /// <summary>
    /// Label/value rows, optionally closed by a bold total row — a fare
    /// breakdown, trip facts. A row's value may be prefixed with "−" or "+";
    /// it is shown as given.
    /// </summary>
    public static string Details(IEnumerable<(string Label, string Value)> rows, (string Label, string Value)? total = null)
    {
        var body = string.Concat(rows.Select(r => $"""
            <tr>
              <td style="padding:10px 0;border-bottom:1px solid {B.Line};color:{B.Muted};font-size:14px">{Encode(r.Label)}</td>
              <td align="right" style="padding:10px 0;border-bottom:1px solid {B.Line};color:{B.Ink};font-size:14px;font-weight:600">{Encode(r.Value)}</td>
            </tr>
            """));

        var totalRow = total is not { } t ? "" : $"""
            <tr>
              <td style="padding:14px 0 4px;color:{B.Ink};font-size:16px;font-weight:800">{Encode(t.Label)}</td>
              <td align="right" style="padding:14px 0 4px;color:{B.Ink};font-size:16px;font-weight:800">{Encode(t.Value)}</td>
            </tr>
            """;

        return $"""<table role="presentation" width="100%" cellpadding="0" cellspacing="0">{body}{totalRow}</table>""";
    }
}
