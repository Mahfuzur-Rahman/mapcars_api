namespace Mapcars.Domain.Entities;

public class VerificationCode
{
    public Guid Id { get; set; }
    public string UserType { get; set; } = string.Empty;   // "rider" | "driver"
    public string Provider { get; set; } = string.Empty;   // "email" | "phone"
    public string Identifier { get; set; } = string.Empty; // email address or phone number
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
