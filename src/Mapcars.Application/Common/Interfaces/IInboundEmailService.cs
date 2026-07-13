namespace Mapcars.Application.Common.Interfaces;

public interface IInboundEmailService
{
    /// <summary>Verifies the Svix signature Resend attaches to inbound webhook requests.</summary>
    bool VerifySignature(string payload, string svixId, string svixTimestamp, string svixSignature);

    /// <summary>Fetches the full received email and relays it to the configured inbound mailbox.</summary>
    Task ForwardReceivedEmailAsync(string emailId, CancellationToken ct = default);
}
