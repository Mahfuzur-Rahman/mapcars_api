using Mapcars.Application.Common.Dtos;
using Mapcars.Application.Drivers.Dtos;

namespace Mapcars.Application.Drivers.Interfaces;

public interface IDriverAuthService
{
    Task ChangePasswordAsync(Guid driverId, ChangePasswordRequest request, CancellationToken ct = default);

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
    Task<DriverProfileResponse> GetProfileAsync(Guid driverId, CancellationToken ct = default);
    Task<DriverProfileResponse> UpdateProfileAsync(Guid driverId, UpdateDriverProfileRequest req, CancellationToken ct = default);
    Task<DriverProfileResponse> UploadProfilePictureAsync(Guid driverId, Stream content, string fileName, string contentType, long fileSize, CancellationToken ct = default);
    Task<(Stream Content, string ContentType)?> GetProfilePictureAsync(Guid driverId, CancellationToken ct = default);
    Task<DriverProfileResponse> SetAvailabilityAsync(Guid driverId, bool isOnline, CancellationToken ct = default);
}
