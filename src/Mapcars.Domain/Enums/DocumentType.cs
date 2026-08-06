namespace Mapcars.Domain.Enums;

/// <summary>
/// Rider identity documents (0-9) and driver licensing/vehicle documents
/// (10+) share one enum so a single Document entity/table can hold both —
/// which values are valid for which role is enforced in DocumentService.
/// </summary>
public enum DocumentType
{
    ProofOfIdentity = 0,
    ProofOfAddress = 1,

    PhvLicence = 10,
    VehicleInsurance = 11,
    VehicleRegistration = 12,
    DbsCheck = 13,

    // Vehicle photos — also driver documents, stored in the same documents table
    // (keyed by driver_id); the Vehicle entity holds the structured details.
    VehicleFrontPhoto = 14,
    VehicleRearPhoto = 15,
    VehicleInteriorPhoto = 16,

    Passport = 17,
    DrivingLicence = 18,

    /// <summary>The vehicle's own PHV licence plate/disc — distinct from the driver's PhvLicence badge.</summary>
    VehicleBadge = 19,
    BankStatement = 20,
}
