namespace Mapcars.Domain.Enums;

/// <summary>
/// Settlement state of a trip's fare. Cash trips go <see cref="Pending"/> →
/// <see cref="Collected"/> when the driver completes the trip. Card trips will
/// add authorize/capture transitions once Stripe charging lands.
/// </summary>
public enum PaymentStatus
{
    Pending,
    Collected,
    Failed,
}
