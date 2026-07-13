namespace Mapcars.Infrastructure.Options;

public class TelnyxOptions
{
    public const string Section = "Sms:Telnyx";

    public string ApiKey { get; init; } = string.Empty;

    // E.164 format, e.g. +447700900000
    public string FromNumber { get; init; } = string.Empty;
}
