using Mapcars.Application.Pricing.Dtos;
using Mapcars.Application.Pricing.Interfaces;
using Mapcars.Application.Pricing.Models;
using ValidationException = Mapcars.Application.Common.Exceptions.ValidationException;
using FluentValidation.Results;

namespace Mapcars.Application.Pricing.Services;

/// <summary>
/// Pricing business logic. Pulls the current chart from the store (hot cache) and
/// runs the shared <see cref="FareCalculator"/>. Route distance/duration coming
/// from a client are clamped so a tampered value can't move the price: distance
/// against a straight-line lower bound and a plausible road-factor upper bound,
/// then duration against a believable average-speed band over that distance.
/// </summary>
public class PricingService : IPricingService
{
    private readonly IFareChartStore _store;

    // A real road route is longer than the straight line, but only so much.
    private const double MinRoadFactor = 0.85;  // GPS/rounding slack below straight-line
    private const double MaxRoadFactor = 3.0;   // implausibly indirect above this → clamp
    private const double TypicalRoadFactor = 1.35; // used when the client sends nothing
    private const double MinRouteMiles = 0.5;   // floor on the upper bound for very short hops
    private const double MilesPerMeter = 1 / 1609.344;

    // And the duration has to fit the distance: an average speed outside this band
    // is not a real route, it is someone pricing the per-minute part to zero.
    private const double MaxAverageMph = 70.0;  // motorway pace, end to end
    private const double MinAverageMph = 3.0;   // walking pace — slower than any gridlock
    private const double MinDurationCeilingMinutes = 5.0; // a short hop can still sit at the lights

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

    public async Task<PricedTier> PriceTierAsync(
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

        return new PricedTier(breakdown, miles, minutes, chart.Version);
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
            double hi = Math.Max(straightMiles * MaxRoadFactor, MinRouteMiles);
            miles = Math.Clamp(clientMiles, lo, hi);
        }

        // Clamped against the CLAMPED distance, so a tampered distance can't
        // widen the duration band either.
        double minutes = ClampMinutes(miles, clientMinutes);

        return (miles, minutes);
    }

    /// <summary>
    /// Clamp a client-supplied duration to a believable average speed over
    /// <paramref name="miles"/>. The per-minute rate is part of the fare, so an
    /// unchecked duration is an unchecked price: 0.01 minutes erases that part.
    /// </summary>
    public static double ClampMinutes(double miles, double clientMinutes)
    {
        // Missing: estimate from distance at ~18 mph average city speed. That is
        // inside the band below by construction, so it needs no clamp.
        if (clientMinutes <= 0) return miles / 18.0 * 60.0;

        double lo = miles / MaxAverageMph * 60.0;
        double hi = Math.Max(miles / MinAverageMph * 60.0, MinDurationCeilingMinutes);
        return Math.Clamp(clientMinutes, lo, hi);
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
