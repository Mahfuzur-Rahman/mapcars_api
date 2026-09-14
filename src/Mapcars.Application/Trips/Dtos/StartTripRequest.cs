namespace Mapcars.Application.Trips.Dtos;

/// <summary>
/// Starting a trip. The PIN is the 4-digit code the passenger reads out at the
/// kerb, and it is verified by the SERVER — the driver app checking it locally
/// proved nothing.
///
/// <para>
/// Optional, and the whole body is optional, because a driver build older than
/// this change posts nothing at all. Refusing those would strand live trips
/// mid-shift. Once every build sends a PIN, make it required.
/// </para>
/// </summary>
public class StartTripRequest
{
    public string? Pin { get; set; }
}
