using Mapcars.Application.Pricing.Models;

namespace Mapcars.Infrastructure.Pricing;

/// <summary>
/// The seed fare chart used the very first time the platform runs (nothing in
/// Redis or Postgres yet). Once seeded it lives in the database and is edited via
/// the admin endpoint — this is only a starting point. London-centred.
/// </summary>
public static class FareChartDefaults
{
    public static FareChart Build() => new()
    {
        Version = 1,
        Currency = "GBP",
        UpdatedAtUtc = DateTime.UtcNow,
        Base = new FareBase { BookingFeePence = 0, MinimumFarePence = 500 },
        Rates = new FareRates { PerMilePence = 130, PerMinutePence = 15 },
        Tiers =
        [
            new FareTier { Id = "economy", Name = "Economy", Description = "Everyday rides",
                Icon = "car",  BaseFarePence = 250, Multiplier = 1.0m, Capacity = 4, EtaMinutes = 3 },
            new FareTier { Id = "comfort", Name = "Comfort", Description = "Newer cars, more room",
                Icon = "car",  BaseFarePence = 300, Multiplier = 1.35m, Capacity = 4, EtaMinutes = 5 },
            new FareTier { Id = "xl", Name = "XL", Description = "Up to 6 seats",
                Icon = "car",  BaseFarePence = 350, Multiplier = 1.7m, Capacity = 6, EtaMinutes = 6 },
            new FareTier { Id = "premium", Name = "Premium", Description = "Top-rated drivers",
                Icon = "bolt", BaseFarePence = 400, Multiplier = 2.1m, Capacity = 4, EtaMinutes = 4 },
        ],
        Modifiers = new FareModifiers
        {
            RushHour =
            [
                new RushHourRule { Days = [1, 2, 3, 4, 5], From = "07:00", To = "10:00", Multiplier = 1.25m },
                new RushHourRule { Days = [1, 2, 3, 4, 5], From = "16:00", To = "19:00", Multiplier = 1.25m },
            ],
            Zones =
            [
                new ZoneSurcharge { Id = "heathrow", Type = "airport",
                    Lat = 51.4700, Lng = -0.4543, RadiusM = 3000, SurchargePence = 500 },
            ],
            BusyAreas = [],
            OutsideCity = new OutsideCityRule
            {
                CityLat = 51.5090, CityLng = -0.1260, RadiusM = 25000, Multiplier = 1.15m,
            },
        },
        Platform = new PlatformConfig { DriverFeePercent = 15m },
    };
}
