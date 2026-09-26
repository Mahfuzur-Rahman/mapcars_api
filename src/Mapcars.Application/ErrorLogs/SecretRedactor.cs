using System.Text.RegularExpressions;

namespace Mapcars.Application.ErrorLogs;

/// <summary>
/// Strips credentials out of text before it is written to <c>error_logs</c>.
///
/// <para>
/// The error log is readable by every admin and is kept for months. Exception
/// messages routinely carry the secret that caused them — a Stripe SDK error can
/// echo the key it was called with, a failed Npgsql open includes the connection
/// string, an HTTP client error includes the URL with its <c>?key=</c>. Once such
/// a row is written, the key is effectively rotated-by-necessity, so this runs on
/// every write rather than trusting call sites to remember.
/// </para>
///
/// <para>
/// Deliberately over-eager: a redacted identifier costs a little debugging; a
/// leaked live key costs money. Publishable keys (<c>pk_</c>) are public by design
/// and left alone so logs stay useful.
/// </para>
/// </summary>
public static class SecretRedactor
{
    public const string Mask = "[REDACTED]";

    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);

    private static readonly (Regex Pattern, string Replacement)[] Rules =
    [
        // PEM blocks — the Firebase service account's private key.
        (Rx(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?(-----END [A-Z ]*PRIVATE KEY-----|$)"), Mask),

        // Stripe secret / restricted keys, webhook signing secrets, and the
        // client secrets on PaymentIntents and SetupIntents.
        (Rx(@"\b(sk|rk)_(live|test)_[0-9A-Za-z]{8,}"), Mask),
        (Rx(@"\bwhsec_[0-9A-Za-z]{8,}"), Mask),
        (Rx(@"\b(pi|seti)_[0-9A-Za-z]+_secret_[0-9A-Za-z]+"), Mask),

        // Resend API keys.
        (Rx(@"\bre_[0-9A-Za-z]{6,}_[0-9A-Za-z]{6,}"), Mask),

        // Google API keys (Maps, Firebase web config).
        (Rx(@"\bAIza[0-9A-Za-z\-_]{35}"), Mask),

        // Bearer headers and bare JWTs (our access tokens and Google ID tokens).
        (Rx(@"(?i)\bbearer\s+[0-9A-Za-z\-_\.=+/]{10,}"), "Bearer " + Mask),
        (Rx(@"\beyJ[0-9A-Za-z\-_]+\.[0-9A-Za-z\-_]+\.[0-9A-Za-z\-_]*"), Mask),

        // Connection strings: Password=...; / Pwd=...
        (Rx(@"(?i)\b(password|pwd)\s*=\s*[^;""'\s]+"), "$1=" + Mask),

        // postgres://user:password@host
        (Rx(@"(?i)\b([a-z][a-z0-9+\-.]*://[^:/\s@]+):[^@\s/]+@"), "$1:" + Mask + "@"),

        // Secret-looking query parameters, e.g. ?key=…&access_token=…
        (Rx(@"(?i)([?&](?:api_?key|key|token|access_token|refresh_token|secret|client_secret|password|signature|sig)=)[^&\s""'#]+"),
            "$1" + Mask),
    ];

    /// <summary>Returns <paramref name="text"/> with anything credential-shaped masked.</summary>
    public static string? Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        foreach (var (pattern, replacement) in Rules)
        {
            try
            {
                text = pattern.Replace(text, replacement);
            }
            catch (RegexMatchTimeoutException)
            {
                // Pathological input. Refusing to store it beats storing it raw.
                return "(message withheld: could not be scanned for secrets)";
            }
        }

        return text;
    }

    private static Regex Rx(string pattern) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, Timeout);
}
