using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// The vehicle a driver operates. One row per driver (a driver registers a
/// single active vehicle). Vehicle photos are not stored here — they live in the
/// documents table as driver documents (see DocumentType.Vehicle*Photo).
/// </summary>
public class Vehicle : BaseEntity
{
    public Guid DriverId { get; set; }
    public Driver? Driver { get; set; }

    public required string Make { get; set; }
    public required string Model { get; set; }
    public int Year { get; set; }
    public required string Colour { get; set; }

    /// <summary>UK number plate, stored upper-cased/normalised.</summary>
    public required string RegistrationNumber { get; set; }

    /// <summary>The vehicle's own council-issued PHV licence plate number (distinct from the driver's PHV licence).</summary>
    public string? PhvLicencePlateNumber { get; set; }

    /// <summary>The council or authority (e.g. TfL) that issued the PHV vehicle licence.</summary>
    public string? PhvLicensingAuthority { get; set; }

    /// <summary>The vehicle's assigned ride tier (e.g. economy, comfort, xl, premium). Default is economy; set/approved by admin.</summary>
    public string Tier { get; set; } = "economy";
}
