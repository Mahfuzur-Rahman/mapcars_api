using Mapcars.Application.Pricing.Dtos;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Pricing.Models;
using ValidationException = Mapcars.Application.Common.Exceptions.ValidationException;
using FluentValidation.Results;

namespace Mapcars.Application.Pricing.Services;

/// <summary>
/// Pricing business logic. Pulls the current chart from the store (hot cache) and
/// runs the shared <see cref="FareCalculator"/>. Route distance/duration coming
/// from a client are clamped against a straight-line lower bound and a plausible
/// road-factor upper bound so a tampered value can't move the price.
/// </summary>
public class PricingService : IPricingService
{
    private readonly IFareChartStore _store;

    // A real road route is longer than the straight line, but only so much.
    private const double MinRoadFactor = 0.85;  // GPS/rounding slack below straight-line
    private const double MaxRoadFactor = 3.0;   // implausibly indirect above this → clamp
    private const double TypicalRoadFactor = 1.35; // used when the client sends nothing
    private const double MilesPerMeter = 1 / 1609.344;

    public PricingService(IFareChartStore store) => _store = store;

    public Task<FareChart> GetChartAsync(CancellationToken ct = default)
        => _store.GetCurrentAsync(ct);

    public Task<FareChart> UpdateChartAsync(FareChart chart, CancellationToken ct = default)
        => _store.SetCurrentAsync(chart, ct);

    public async Task<FareQuoteResponse> QuoteAsync(FareQuoteRequest req, CancellationToken ct = default)
    {
        var chart = await _store.GetCurrentAsync(ct);

        var (miles, minutes) = Normalize(
            req.PickupLat, req.PickupLng, req.DropoffLat, req.DropoffLng,
            req.DistanceMiles, req.DurationMinutes);

        var breakdowns = FareCalculator.CalculateAll(
            chart, miles, minutes,
            req.PickupLat, req.PickupLng, req.DropoffLat, req.DropoffLng,
            UkLocalNow());

        var options = breakdowns
            .Select(b => new TierQuote(
                b.TierId, b.TierId, b.Name, b.EtaMinutes, b.FarePence, b.Description, b.Icon))
            .ToList();

        return new FareQuoteResponse(Math.Round(miles, 1), (int)Math.Round(minutes), options);
    }

    public async Task<FareBreakdown> PriceTierAsync(
        string tierId,
        double pickupLat, double pickupLng,
        double dropoffLat, double dropoffLng,
        double distanceMiles, double durationMinutes,
        CancellationToken ct = default)
    {
        var chart = await _store.GetCurrentAsync(ct);

        var (miles, minutes) = Normalize(
            pickupLat, pickupLng, dropoffLat, dropoffLng, distanceMiles, durationMinutes);

        var breakdown = FareCalculator.Calculate(
            chart, tierId, miles, minutes,
            pickupLat, pickupLng, dropoffLat, dropoffLng, UkLocalNow());

        if (breakdown is null)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(tierId), $"Unknown ride tier '{tierId}'.")
            });
        }

        return breakdown;
    }

    /// <summary>
    /// Clamp client-supplied route metrics to a plausible range around the
    /// straight-line distance between the two points.
    /// </summary>
    private static (double miles, double minutes) Normalize(
        double pLat, double pLng, double dLat, double dLng,
        double clientMiles, double clientMinutes)
    {
        double straightMiles = FareCalculator.HaversineMeters(pLat, pLng, dLat, dLng) * MilesPerMeter;

        double miles;
        if (clientMiles <= 0)
        {
            miles = straightMiles * TypicalRoadFactor;
        }
        else
        {
            double lo = straightMiles * MinRoadFactor;
            double hi = Math.Max(straightMiles * MaxRoadFactor, 0.5); // floor for very short hops
            miles = Math.Clamp(clientMiles, lo, hi);
        }

        // Duration can't be trusted for money but shouldn't be negative; if missing,
        // estimate from distance at ~18 mph average city speed.
        double minutes = clientMinutes > 0 ? clientMinutes : miles / 18.0 * 60.0;

        return (miles, minutes);
    }

    /// <summary>UK local time for time-of-day rules (rush hour), DST-aware.</summary>
    private static DateTime UkLocalNow()
    {
        var utc = DateTime.UtcNow;
        foreach (var id in new[] { "Europe/London", "GMT Standard Time" })
        {
            try { return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(id)); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return utc;
    }
}
