namespace Mapcars.Application.Pricing.Dtos;

/// <summary>
/// Priced route: overall distance/duration plus one <see cref="TierQuote"/> per
/// bookable tier. Shapes match the Flutter <c>RideQuote</c>/<c>RideOption</c> so
/// the client parses it directly.
/// </summary>
public record FareQuoteResponse(
    double DistanceMiles,
    int EtaMinutes,
    IReadOnlyList<TierQuote> Options);

/// <summary>One tier's price for a route. <c>PricePence</c> is integer money.</summary>
public record TierQuote(
    string Id,
    string Tier,
    string Name,
    int EtaMinutes,
    int PricePence,
    string Description,
    string Icon);
