namespace Mapcars.Infrastructure.Options;

public class ResendOptions
{
    public const string Section = "Email:Resend";

    public string ApiKey { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "MAP CARS";
    public string Bcc { get; init; } = string.Empty;

    // Inbound (receiving) — verifies the "email.received" webhook and forwards to a mailbox.
    public string WebhookSecret { get; init; } = string.Empty;
    public string InboundForwardTo { get; init; } = string.Empty;
}
