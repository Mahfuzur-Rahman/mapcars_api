namespace Mapcars.Application.Riders.Dtos;

/// <summary>Inbound payload for registering a rider.</summary>
public record CreateRiderRequest(string FullName, string Email, string PhoneNumber);
