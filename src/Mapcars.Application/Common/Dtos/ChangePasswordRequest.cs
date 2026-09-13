namespace Mapcars.Application.Common.Dtos;

/// <summary>Shared by admin, customer, and driver self-service password change.</summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
