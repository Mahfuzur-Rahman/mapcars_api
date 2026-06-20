namespace Mapcars.Application.Riders.Dtos;

/// <summary>Outbound rider representation. Never expose entities directly.</summary>
public record RiderResponse(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    bool IsActive,
    DateTime CreatedAtUtc);
