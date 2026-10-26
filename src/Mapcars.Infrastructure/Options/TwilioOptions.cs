namespace Mapcars.Infrastructure.Options;

public class TwilioOptions
{
    public const string Section = "Sms:Twilio";

    public string AccountSid { get; init; } = string.Empty;
    public string AuthToken { get; init; } = string.Empty;

    // Who the SMS is from. For UK-only sending this is the text sender name
    // "MAPCARS" (up to 11 characters, one-way, no registration needed in the
    // UK). An E.164 Twilio number also works.
    public string FromNumber { get; init; } = string.Empty;
}
