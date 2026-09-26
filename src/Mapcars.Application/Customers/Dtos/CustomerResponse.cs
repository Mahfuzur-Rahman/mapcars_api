namespace Mapcars.Application.Customers.Dtos;

/// <summary>Outbound customer representation. Never expose entities directly.</summary>
public record CustomerResponse(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    bool IsActive,
    DateTime CreatedAtUtc);
