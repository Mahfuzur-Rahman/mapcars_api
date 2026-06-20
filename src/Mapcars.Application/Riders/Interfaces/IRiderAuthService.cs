using Mapcars.Application.Common.Dtos;

namespace Mapcars.Application.Riders.Interfaces;

public interface IRiderAuthService
{
    // Phone flow
    Task<OtpSentResponse> SendPhoneOtpAsync(string phone, CancellationToken ct = default);
    Task<AuthResponse> VerifyPhoneOtpAsync(string phone, string code, CancellationToken ct = default);

    // Email flow
    Task<OtpSentResponse> SignUpWithEmailAsync(string email, string password, string fullName, CancellationToken ct = default);
    Task<AuthResponse> VerifyEmailOtpAsync(string email, string code, CancellationToken ct = default);
    Task<AuthResponse> LoginWithEmailAsync(string email, string password, CancellationToken ct = default);

    // Google flow
    Task<AuthResponse> SignInWithGoogleAsync(string idToken, CancellationToken ct = default);

    // Profile
    Task<AuthResponse> UpdateProfileAsync(Guid riderId, string fullName, string? email, CancellationToken ct = default);
}
