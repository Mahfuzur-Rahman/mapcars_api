using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mapcars.Infrastructure.Services;

public class TelnyxSmsService(
    IOptions<TelnyxOptions> options,
    ILogger<TelnyxSmsService> logger) : ISmsService
{
    private static readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.telnyx.com/") };
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly TelnyxOptions _opts = options.Value;

    public string ProviderName => "telnyx";

    public async Task SendAsync(string toPhone, string message, CancellationToken ct = default)
    {
        var payload = new
        {
            from = _opts.FromNumber,
            to = toPhone,
            text = message,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Telnyx API error {Status}: {Body}", (int)response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }

        logger.LogInformation("Telnyx SMS sent to {Phone}", toPhone);
    }
}
