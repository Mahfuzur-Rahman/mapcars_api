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
    string Tier,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Admin direct tier update request.</summary>
public record SetVehicleTierRequest(string Tier);

/// <summary>Driver appeal submission request.</summary>
public record CreateTierAppealRequest(
    string RequestedTier,
    string Reason);

/// <summary>Admin review decision for a tier appeal.</summary>
public record ReviewTierAppealRequest(
    string Status,
    string? AdminNotes);

/// <summary>Driver tier appeal detail response.</summary>
public record VehicleTierAppealResponse(
    Guid Id,
    Guid DriverId,
    Guid VehicleId,
    string CurrentTier,
    string RequestedTier,
    string Reason,
    IReadOnlyList<string> PhotoUrls,
    string Status,
    string? AdminNotes,
    DateTime? ReviewedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>Admin tier appeal list item.</summary>
public record TierAppealListItem(
    Guid Id,
    Guid DriverId,
    string? DriverName,
    string? DriverEmail,
    string? DriverPhone,
    Guid VehicleId,
    string VehicleDescription,
    string RegistrationNumber,
    string CurrentTier,
    string RequestedTier,
    string Reason,
    int PhotoCount,
    string Status,
    string? AdminNotes,
    DateTime? ReviewedAtUtc,
    DateTime CreatedAtUtc);
