namespace Mapcars.Infrastructure.Options;

public class TwilioOptions
{
    public const string Section = "Sms:Twilio";

    public string AccountSid { get; init; } = string.Empty;
    public string AuthToken { get; init; } = string.Empty;

    // E.164 format, e.g. +447700900000 or a Twilio Messaging Service SID
    public string FromNumber { get; init; } = string.Empty;
}
