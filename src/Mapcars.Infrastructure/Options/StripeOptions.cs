namespace Mapcars.Infrastructure.Options;

public class StripeOptions
{
    public const string Section = "Stripe";

    public string SecretKey { get; init; } = string.Empty;
    public string WebhookSecret { get; init; } = string.Empty;
}
