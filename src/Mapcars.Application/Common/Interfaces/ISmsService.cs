namespace Mapcars.Application.Common.Interfaces;

public interface ISmsService
{
    string ProviderName { get; }
    Task SendAsync(string toPhone, string message, CancellationToken ct = default);
}
