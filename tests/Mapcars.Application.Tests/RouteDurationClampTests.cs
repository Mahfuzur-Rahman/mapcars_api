using Mapcars.Application.Pricing.Services;

namespace Mapcars.Application.Tests;

/// <summary>
/// The duration half of the route clamp.
///
/// The per-minute part of a fare is priced on a duration the client sends. Left
/// unchecked, <c>DurationMinutes = 0.01</c> erases it; so the duration has to
/// describe a believable average speed over the (already clamped) distance:
/// no faster than 70 mph, no slower than 3 mph, and never capped below 5 minutes.
/// </summary>
public class RouteDurationClampTests
{
    private const int Precision = 6;

    [Fact]
    public void A_duration_faster_than_70_mph_is_raised_to_70_mph()
    {
        // 10 miles in 1 minute is 600 mph. At 70 mph it takes 10/70*60 ≈ 8.57 min.
        Assert.Equal(10.0 / 70 * 60, PricingService.ClampMinutes(10, 1), Precision);
    }

    [Fact]
    public void A_near_zero_duration_cannot_erase_the_per_minute_fare()
    {
        Assert.Equal(10.0 / 70 * 60, PricingService.ClampMinutes(10, 0.01), Precision);
    }

    [Theory]
    [InlineData(10, 30)]   // 20 mph — ordinary city driving
    [InlineData(10, 9)]    // ~67 mph — motorway, just inside the ceiling
    [InlineData(10, 180)]  // ~3.3 mph — gridlock, just inside the floor
    public void A_believable_duration_is_priced_as_sent(double miles, double minutes)
        => Assert.Equal(minutes, PricingService.ClampMinutes(miles, minutes), Precision);

    [Fact]
    public void A_duration_slower_than_3_mph_is_capped_at_3_mph()
    {
        // 10 miles in 10 hours is 1 mph. At 3 mph it takes 10/3*60 = 200 min.
        Assert.Equal(200, PricingService.ClampMinutes(10, 600), Precision);
    }

    [Fact]
    public void A_very_short_trip_may_still_take_five_minutes()
    {
        // 0.1 mi at 3 mph is 2 minutes — but a short hop can sit at one set of
        // lights for longer than that, so the ceiling never drops below 5.
        Assert.Equal(4.5, PricingService.ClampMinutes(0.1, 4.5), Precision);
        Assert.Equal(5, PricingService.ClampMinutes(0.1, 30), Precision);
    }

    [Fact]
    public void A_missing_duration_is_estimated_from_the_distance()
    {
        // Unchanged behaviour: no duration means ~18 mph average city speed.
        Assert.Equal(10.0 / 18 * 60, PricingService.ClampMinutes(10, 0), Precision);
    }
}
