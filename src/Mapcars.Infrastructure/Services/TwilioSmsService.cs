using Mapcars.Application.Common.Interfaces;
using Mapcars.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Mapcars.Infrastructure.Services;

public class TwilioSmsService(
    IOptions<TwilioOptions> options,
    ILogger<TwilioSmsService> logger) : ISmsService
{
    private readonly TwilioOptions _opts = options.Value;

    public string ProviderName => "twilio";

    public async Task SendAsync(string toPhone, string message, CancellationToken ct = default)
    {
        TwilioClient.Init(_opts.AccountSid, _opts.AuthToken);

        var result = await MessageResource.CreateAsync(
            to: new PhoneNumber(toPhone),
            from: new PhoneNumber(_opts.FromNumber),
            body: message);

        logger.LogInformation("SMS sent to {Phone} — SID: {Sid} Status: {Status}",
            toPhone, result.Sid, result.Status);
    }
}
