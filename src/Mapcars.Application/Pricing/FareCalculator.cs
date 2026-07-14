using Mapcars.Application.Pricing.Models;

namespace Mapcars.Application.Pricing;

/// <summary>Per-tier priced result. All money in integer pence.</summary>
public record FareBreakdown(
    string TierId,
    string Name,
    string Description,
    string Icon,
    int EtaMinutes,
    int FarePence,
    int PlatformFeePence,
    int DriverEarningsPence,
    decimal SurgeMultiplier);

/// <summary>
/// Pure fare arithmetic — no I/O, no time source of its own (the caller passes
/// <c>localNow</c>). This exact formula is mirrored in the Flutter clients
/// (customer_app/lib/src/features/ride/services/fare_calculator.dart) so the
/// instant on-device estimate matches the API's authoritative recompute.
///
/// Formula, per tier:
///   subtotal = bookingFee + tier.baseFare + perMile*miles + perMinute*minutes
///   subtotal *= tier.multiplier
///   subtotal *= surge          (rushHour × busyArea × outsideCity)
///   subtotal += zoneSurcharges (airport/station flat add-ons)
///   fare = max(round(subtotal), minimumFare)
///   driverEarnings = fare − round(fare * driverFeePercent/100)
/// </summary>
public static class FareCalculator
{
    public static IReadOnlyList<FareBreakdown> CalculateAll(
        FareChart chart, double miles, double minutes,
        double pickupLat, double pickupLng, double dropoffLat, double dropoffLng,
        DateTime localNow)
        => chart.Tiers
            .Select(t => Calculate(chart, t, miles, minutes, pickupLat, pickupLng, dropoffLat, dropoffLng, localNow))
            .ToList();

    /// <summary>Price a single tier by id, or null if the tier isn't in the chart.</summary>
    public static FareBreakdown? Calculate(
        FareChart chart, string tierId, double miles, double minutes,
        double pickupLat, double pickupLng, double dropoffLat, double dropoffLng,
        DateTime localNow)
    {
        var tier = chart.Tiers.FirstOrDefault(
            t => string.Equals(t.Id, tierId, StringComparison.OrdinalIgnoreCase));
        return tier is null
            ? null
            : Calculate(chart, tier, miles, minutes, pickupLat, pickupLng, dropoffLat, dropoffLng, localNow);
    }

    public static FareBreakdown Calculate(
        FareChart chart, FareTier tier, double miles, double minutes,
        double pickupLat, double pickupLng, double dropoffLat, double dropoffLng,
        DateTime localNow)
    {
        miles = Math.Max(0, miles);
        minutes = Math.Max(0, minutes);

        double subtotal = chart.Base.BookingFeePence
            + tier.BaseFarePence
            + chart.Rates.PerMilePence * miles
            + chart.Rates.PerMinutePence * minutes;

        subtotal *= (double)tier.Multiplier;

        var surge = SurgeMultiplier(chart, pickupLat, pickupLng, localNow);
        subtotal *= (double)surge;

        subtotal += ZoneSurchargePence(chart, pickupLat, pickupLng, dropoffLat, dropoffLng);

        int fare = Math.Max((int)Math.Round(subtotal), chart.Base.MinimumFarePence);

        int platformFee = (int)Math.Round(fare * chart.Platform.DriverFeePercent / 100m);
        int driverEarnings = fare - platformFee;

        return new FareBreakdown(
            tier.Id, tier.Name, tier.Description, tier.Icon, tier.EtaMinutes,
            fare, platformFee, driverEarnings, surge);
    }

    /// <summary>Combined multiplicative surge for a pickup point at a given local time.</summary>
    public static decimal SurgeMultiplier(FareChart chart, double lat, double lng, DateTime localNow)
    {
        decimal m = 1m;

        foreach (var r in chart.Modifiers.RushHour)
            if (MatchesRushHour(r, localNow)) m *= r.Multiplier;

        // First matching busy area wins (bubbles shouldn't stack on themselves).
        foreach (var b in chart.Modifiers.BusyAreas)
            if (WithinMeters(lat, lng, b.Lat, b.Lng, b.RadiusM)) { m *= b.Multiplier; break; }

        var oc = chart.Modifiers.OutsideCity;
        if (oc is not null && !WithinMeters(lat, lng, oc.CityLat, oc.CityLng, oc.RadiusM))
            m *= oc.Multiplier;

        return m;
    }

    static int ZoneSurchargePence(
        FareChart chart, double pLat, double pLng, double dLat, double dLng)
    {
        int total = 0;
        foreach (var z in chart.Modifiers.Zones)
        {
            bool hit = (z.AppliesToPickup && WithinMeters(pLat, pLng, z.Lat, z.Lng, z.RadiusM))
                    || (z.AppliesToDropoff && WithinMeters(dLat, dLng, z.Lat, z.Lng, z.RadiusM));
            if (hit) total += z.SurchargePence;
        }
        return total;
    }

    static bool MatchesRushHour(RushHourRule r, DateTime localNow)
    {
        int iso = ((int)localNow.DayOfWeek + 6) % 7 + 1; // Mon=1 … Sun=7
        if (r.Days.Count > 0 && !r.Days.Contains(iso)) return false;

        if (!TimeSpan.TryParse(r.From, out var from) || !TimeSpan.TryParse(r.To, out var to))
            return false;

        var t = localNow.TimeOfDay;
        return from <= to
            ? (t >= from && t < to)     // same-day window
            : (t >= from || t < to);    // window wraps past midnight
    }

    /// <summary>
    /// Great-circle radius test with a cheap latitude bounding-box prefilter so
    /// far-away zones are rejected without the trig cost.
    /// </summary>
    public static bool WithinMeters(double lat1, double lng1, double lat2, double lng2, int radiusM)
    {
        const double metersPerDegLat = 111_320.0;
        if (Math.Abs(lat1 - lat2) * metersPerDegLat > radiusM + 50) return false;
        return HaversineMeters(lat1, lng1, lat2, lng2) <= radiusM;
    }

    public static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6_371_000.0;
        double p1 = lat1 * Math.PI / 180, p2 = lat2 * Math.PI / 180;
        double dp = (lat2 - lat1) * Math.PI / 180, dl = (lng2 - lng1) * Math.PI / 180;
        double a = Math.Sin(dp / 2) * Math.Sin(dp / 2)
                 + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
