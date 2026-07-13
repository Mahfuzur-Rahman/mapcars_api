using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mapcars.Infrastructure.Services;

/// <summary>
/// Handles Resend's "receiving" feature: verifies the email.received webhook
/// (Svix signature) and relays the full email to the configured mailbox.
/// </summary>
public class ResendInboundEmailService(
    IOptions<ResendOptions> options,
    ILogger<ResendInboundEmailService> logger) : IInboundEmailService
{
    private static readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.resend.com/") };

    private readonly ResendOptions _opts = options.Value;

    public bool VerifySignature(string payload, string svixId, string svixTimestamp, string svixSignature)
    {
        if (string.IsNullOrWhiteSpace(_opts.WebhookSecret))
        {
            logger.LogError("Resend inbound webhook received but Email:Resend:WebhookSecret is not configured");
            return false;
        }

        var secretBytes = Convert.FromBase64String(_opts.WebhookSecret.Replace("whsec_", string.Empty));
        var signedContent = Encoding.UTF8.GetBytes($"{svixId}.{svixTimestamp}.{payload}");

        using var hmac = new HMACSHA256(secretBytes);
        var expected = Convert.ToBase64String(hmac.ComputeHash(signedContent));

        // svix-signature can contain multiple space-separated "v1,<base64>" values.
        foreach (var part in svixSignature.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var comma = part.IndexOf(',');
            if (comma < 0) continue;

            var candidate = part[(comma + 1)..];
            if (CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(candidate), Convert.FromBase64String(expected)))
            {
                return true;
            }
        }

        return false;
    }

    public async Task ForwardReceivedEmailAsync(string emailId, CancellationToken ct = default)
    {
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"emails/receiving/{emailId}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);

        using var getResponse = await _http.SendAsync(getRequest, ct);
        getResponse.EnsureSuccessStatusCode();

        var received = await getResponse.Content.ReadFromJsonAsync<ReceivedEmail>(ct)
            ?? throw new InvalidOperationException($"Resend returned no body for received email {emailId}");

        var preamble = $"""
            <p style="color:#888;font-size:12px;margin:0 0 16px">
              Forwarded from {received.To.FirstOrDefault()} — originally sent by {received.From}
            </p>
            """;

        var payload = new Dictionary<string, object?>
        {
            ["from"] = $"{_opts.FromName} <{_opts.FromAddress}>",
            ["to"] = new[] { _opts.InboundForwardTo },
            ["reply_to"] = received.From,
            ["subject"] = $"Fwd: {received.Subject}",
            ["html"] = preamble + (received.Html ?? $"<pre>{received.Text}</pre>"),
        };

        using var sendRequest = new HttpRequestMessage(HttpMethod.Post, "emails");
        sendRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
        sendRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var sendResponse = await _http.SendAsync(sendRequest, ct);

        if (!sendResponse.IsSuccessStatusCode)
        {
            var error = await sendResponse.Content.ReadAsStringAsync(ct);
            logger.LogError("Resend forward-send error {Status}: {Body}", (int)sendResponse.StatusCode, error);
            sendResponse.EnsureSuccessStatusCode();
        }

        logger.LogInformation("Forwarded received email {EmailId} from {From} to {ForwardTo}",
            emailId, received.From, _opts.InboundForwardTo);
    }

    private class ReceivedEmail
    {
        [JsonPropertyName("from")]
        public string From { get; init; } = string.Empty;

        [JsonPropertyName("to")]
        public List<string> To { get; init; } = [];

        [JsonPropertyName("subject")]
        public string Subject { get; init; } = string.Empty;

        [JsonPropertyName("html")]
        public string? Html { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }
}
