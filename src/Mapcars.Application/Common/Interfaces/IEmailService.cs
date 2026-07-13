namespace Mapcars.Application.Common.Interfaces;

public interface IEmailService
{
    string ProviderName { get; }
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}
