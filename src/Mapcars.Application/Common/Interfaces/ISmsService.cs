namespace Mapcars.Application.Common.Interfaces;

public interface ISmsService
{
    Task SendAsync(string toPhone, string message, CancellationToken ct = default);
}
