using Mapcars.Application.Common.Interfaces;
using Mapcars.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Mapcars.Infrastructure.Services;

public class SmtpEmailService(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly SmtpOptions _opts = options.Value;

    public string ProviderName => "smtp";

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opts.FromName, _opts.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        if (!string.IsNullOrWhiteSpace(_opts.Bcc))
        {
            message.Bcc.Add(MailboxAddress.Parse(_opts.Bcc));
        }
        message.Subject = subject;

        message.Body = new BodyBuilder
        {
            HtmlBody = EmailTemplate.Wrap(subject, body),
        }.ToMessageBody();

        using var client = new SmtpClient();

        var socketOptions = _opts.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        await client.ConnectAsync(_opts.Host, _opts.Port, socketOptions, ct);
        await client.AuthenticateAsync(_opts.User, _opts.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);

        logger.LogInformation("SMTP email sent to {Email} — {Subject}", toEmail, subject);
    }
}
