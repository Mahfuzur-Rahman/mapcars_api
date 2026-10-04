using System.Net;

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
/// </summary>
public static class EmailHtml
{
    /// <summary>Safe in element content and in quoted attribute values (quotes are encoded too).</summary>
    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
