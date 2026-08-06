namespace Mapcars.Application.Vehicles.Dtos;

/// <summary>Create-or-update the authenticated driver's vehicle.</summary>
public record UpsertVehicleRequest(
    string Make,
    string Model,
    int Year,
    string Colour,
    string RegistrationNumber,
    string? PhvLicencePlateNumber = null,
    string? PhvLicensingAuthority = null);

/// <summary>Outbound vehicle representation. Never expose the entity directly.</summary>
public record VehicleResponse(
    Guid Id,
    string Make,
    string Model,
    int Year,
    string Colour,
    string RegistrationNumber,
    string? PhvLicencePlateNumber,
    string? PhvLicensingAuthority,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
