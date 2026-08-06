namespace Mapcars.Application.Common.Dtos;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; }
    public string UserType { get; set; } = string.Empty; // "rider" | "driver"
    public Guid UserId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsProfileComplete { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
}

public class OtpSentResponse
{
    public string Message { get; set; } = string.Empty;
    /// <summary>
    /// The OTP, echoed back only as a local-dev convenience. Enforced null outside
    /// Development by the auth services (gated on <c>IAppEnvironment.IsDevelopment</c>) —
    /// returning it in Production would let anyone take over any account.
    /// </summary>
    public string? DevCode { get; set; }
}
