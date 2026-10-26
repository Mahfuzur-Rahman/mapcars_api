using Mapcars.Application.Common.Interfaces;
using Mapcars.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio.Clients;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Mapcars.Infrastructure.Services;

/// <summary>
/// Sends SMS through Twilio Programmable Messaging. It is a singleton holding
/// one <see cref="TwilioRestClient"/>. The old version re-initialised Twilio's
/// process-wide static client on every send, and a client per request would
/// mean an HttpClient per request.
/// </summary>
public class TwilioSmsService : ISmsService
{
    private readonly TwilioRestClient _client;
    private readonly PhoneNumber _from;
    private readonly ILogger<TwilioSmsService> _logger;

    public TwilioSmsService(IOptions<TwilioOptions> options, ILogger<TwilioSmsService> logger)
    {
        var opts = options.Value;
        _client = new TwilioRestClient(opts.AccountSid, opts.AuthToken);
        _from = new PhoneNumber(opts.FromNumber);
        _logger = logger;
    }

    public string ProviderName => "twilio";

    public async Task SendAsync(string toPhone, string message, CancellationToken ct = default)
    {
        // Twilio 7.8's static CreateAsync takes no CancellationToken. A send
        // already in flight completes, and the caller's token still bounds
        // everything around it.
        var result = await MessageResource.CreateAsync(
            to: new PhoneNumber(toPhone),
            from: _from,
            body: message,
            client: _client);

        _logger.LogInformation("SMS sent to {Phone}: SID {Sid}, status {Status}",
            Mask(toPhone), result.Sid, result.Status);
    }

    private static string Mask(string phone)
        => phone.Length > 6 ? phone[..3] + new string('*', phone.Length - 6) + phone[^3..] : "***";
}
