using Mapcars.Application.Common.Dtos;
using Mapcars.Application.Riders.Dtos;

namespace Mapcars.Application.Riders.Interfaces;

public interface IRiderAuthService
{
    Task ChangePasswordAsync(Guid riderId, ChangePasswordRequest request, CancellationToken ct = default);

    // Phone flow
    Task<OtpSentResponse> SendPhoneOtpAsync(string phone, CancellationToken ct = default);
    Task<AuthResponse> VerifyPhoneOtpAsync(string phone, string code, CancellationToken ct = default);

    // Email flow
    Task<OtpSentResponse> SignUpWithEmailAsync(string email, string password, string fullName, CancellationToken ct = default);
    Task<OtpSentResponse> ResendEmailOtpAsync(string email, CancellationToken ct = default);
    Task<AuthResponse> VerifyEmailOtpAsync(string email, string code, CancellationToken ct = default);
    Task<AuthResponse> LoginWithEmailAsync(string email, string password, CancellationToken ct = default);

    // Google flow
    /// <summary>
    /// Google sign-in. With <paramref name="signUp"/> false (the sign-in screen)
    /// an unknown Google account is rejected with "please sign up first" instead
    /// of being turned into a new account.
    /// </summary>
    Task<AuthResponse> SignInWithGoogleAsync(string idToken, bool signUp = false, CancellationToken ct = default);

    // Profile
    Task<RiderProfileResponse> GetProfileAsync(Guid riderId, CancellationToken ct = default);
    Task<RiderProfileResponse> UpdateProfileAsync(Guid riderId, UpdateProfileRequest request, CancellationToken ct = default);
}
