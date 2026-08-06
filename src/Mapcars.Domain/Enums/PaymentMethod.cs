namespace Mapcars.Domain.Enums;

/// <summary>
/// How the rider pays for a trip. <see cref="Cash"/> is settled in person (no
/// money moves through the platform — used for testing the ride loop without a
/// real charge); <see cref="Card"/> is charged via Stripe (test/live).
/// </summary>
public enum PaymentMethod
{
    Cash,
    Card,
}
