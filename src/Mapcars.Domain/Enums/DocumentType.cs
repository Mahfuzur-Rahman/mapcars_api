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
}
