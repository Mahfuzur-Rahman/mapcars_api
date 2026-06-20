namespace Mapcars.Application.Common.Interfaces;

public interface IOtpService
{
    Task<string> CreateAndSendPhoneOtpAsync(string userType, string phone, CancellationToken ct = default);
    Task<string> CreateAndSendEmailOtpAsync(string userType, string email, CancellationToken ct = default);
    Task<bool> VerifyAsync(string userType, string provider, string identifier, string code, CancellationToken ct = default);
}
