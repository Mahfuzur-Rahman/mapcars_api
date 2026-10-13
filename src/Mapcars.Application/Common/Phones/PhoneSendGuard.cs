namespace Mapcars.Application.Common.Phones;

public enum SendRefusal { None, Cooldown, Hourly, Daily, PlatformCap }

/// <summary>Whether a code may be sent now, and if not, how long until it may.</summary>
public readonly record struct SendDecision(SendRefusal Refusal, int RetryAfterSeconds)
{
    public bool Refused => Refusal != SendRefusal.None;

    public string Message => Refusal switch
    {
        SendRefusal.None => string.Empty,
        SendRefusal.PlatformCap => "We can't send codes right now. Please try again later, or sign in with email.",
        _ => RetryAfterSeconds < 120
            ? $"Please wait {RetryAfterSeconds} seconds before asking for another code."
            : $"Please wait {(RetryAfterSeconds + 59) / 60} minutes before asking for another code.",
    };
}

/// <summary>
/// The send limits for sign-in codes (spec §1.2), as a pure decision so they
/// can be tested without a database. <c>OtpService</c> supplies the history from
/// <c>verification_codes</c>.
/// </summary>
public static class PhoneSendGuard
{
    public const int CooldownSeconds = 60;
    public const int HourlyLimit = 5;
    public const int DailyLimit = 10;

    /// <summary>How long a refusal for the platform cap asks clients to wait.</summary>
    private const int PlatformCapRetrySeconds = 15 * 60;

    /// <param name="sentToNumberLast24h">Every code created for this number in the last 24 h, any user type, failed sends included.</param>
    /// <param name="platformSentLast24h">Real SMS codes created platform-wide in the last 24 h (test numbers excluded).</param>
    public static SendDecision Decide(
        DateTime nowUtc,
        IReadOnlyCollection<DateTime> sentToNumberLast24h,
        int platformSentLast24h,
        int dailySendCap)
    {
        if (platformSentLast24h >= dailySendCap)
            return new SendDecision(SendRefusal.PlatformCap, PlatformCapRetrySeconds);

        var refusal = SendRefusal.None;
        var wait = TimeSpan.Zero;

        void Consider(SendRefusal why, TimeSpan until)
        {
            if (until > wait) { wait = until; refusal = why; }
        }

        if (sentToNumberLast24h.Count > 0)
        {
            var last = sentToNumberLast24h.Max();
            Consider(SendRefusal.Cooldown, last.AddSeconds(CooldownSeconds) - nowUtc);
        }

        // The oldest send that still counts against a limit: once it leaves the
        // window, a slot frees up.
        var inHour = sentToNumberLast24h.Where(t => t > nowUtc.AddHours(-1)).OrderBy(t => t).ToList();
        if (inHour.Count >= HourlyLimit)
            Consider(SendRefusal.Hourly, inHour[inHour.Count - HourlyLimit].AddHours(1) - nowUtc);

        var inDay = sentToNumberLast24h.Where(t => t > nowUtc.AddDays(-1)).OrderBy(t => t).ToList();
        if (inDay.Count >= DailyLimit)
            Consider(SendRefusal.Daily, inDay[inDay.Count - DailyLimit].AddDays(1) - nowUtc);

        return refusal == SendRefusal.None
            ? new SendDecision(SendRefusal.None, 0)
            : new SendDecision(refusal, Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)));
    }
}
