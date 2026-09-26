using Mapcars.Application.Auth.Dtos;

namespace Mapcars.Application.Auth.Interfaces;

/// <summary>Self-service "forgot password" for customers and drivers (admins are reset by a SuperAdmin).</summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Emails a reset code if the account exists. Completes identically whether or
    /// not it does, so the endpoint can't be used to test which emails are registered.
    /// </summary>
    Task RequestAsync(ForgotPasswordRequest request, CancellationToken ct = default);

    /// <summary>Sets the new password and signs the account out everywhere.</summary>
    Task ResetAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
