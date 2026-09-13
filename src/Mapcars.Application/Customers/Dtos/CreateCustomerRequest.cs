namespace Mapcars.Application.Customers.Dtos;

/// <summary>Inbound payload for registering a customer.</summary>
public record CreateCustomerRequest(string FullName, string Email, string PhoneNumber);
