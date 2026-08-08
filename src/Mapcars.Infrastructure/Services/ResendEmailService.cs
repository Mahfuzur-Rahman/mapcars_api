using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mapcars.Infrastructure.Services;

public class ResendEmailService(
    IOptions<ResendOptions> options,
    ILogger<ResendEmailService> logger) : IEmailService
{
    private static readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.resend.com/") };
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ResendOptions _opts = options.Value;

    public string ProviderName => "resend";
    public string DefaultFromAddress => _opts.FromAddress;
    public string DefaultFromName => _opts.FromName;

    public async Task SendAsync(
        string toEmail, string subject, string body, EmailSendOptions? options = null, CancellationToken ct = default)
    {
        var fromAddress = options?.FromAddress ?? _opts.FromAddress;
        var fromName = options?.FromName ?? _opts.FromName;

        var payload = new
        {
            from = $"{fromName} <{fromAddress}>",
            to = new[] { toEmail },
            bcc = string.IsNullOrWhiteSpace(_opts.Bcc) ? null : new[] { _opts.Bcc },
            subject,
            html = EmailTemplate.Wrap(subject, body),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Resend API error {Status}: {Body}", (int)response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }

        logger.LogInformation("Resend email sent to {Email} — {Subject}", toEmail, subject);
    }
}
