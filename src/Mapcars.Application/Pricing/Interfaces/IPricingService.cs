using Mapcars.Application.Pricing.Dtos;
using Mapcars.Application.Pricing.Models;

namespace Mapcars.Application.Pricing.Interfaces;

/// <summary>Pricing use-cases: read/update the chart, quote a route, price a tier.</summary>
public interface IPricingService
{
    /// <summary>The current fare chart (served to clients for local estimates).</summary>
    Task<FareChart> GetChartAsync(CancellationToken ct = default);

    /// <summary>Publish a new fare chart (admin). Returns it with its new version.</summary>
    Task<FareChart> UpdateChartAsync(FareChart chart, CancellationToken ct = default);

    /// <summary>Price every tier for a route (client quote path).</summary>
    Task<FareQuoteResponse> QuoteAsync(FareQuoteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Authoritatively price one chosen tier for a route (booking path). Route
    /// metrics are clamped against a straight-line sanity check first. Throws if
    /// the tier isn't in the current chart.
    /// </summary>
    Task<FareBreakdown> PriceTierAsync(
        string tierId,
        double pickupLat, double pickupLng,
        double dropoffLat, double dropoffLng,
        double distanceMiles, double durationMinutes,
        CancellationToken ct = default);
}
