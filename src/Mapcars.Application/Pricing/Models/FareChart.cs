namespace Mapcars.Application.Pricing.Models;

/// <summary>
/// The complete pricing configuration — the "fare chart". Stored in Redis (hot
/// cache) + Postgres (durable audit), and served to clients over HTTP so they can
/// compute an instant local estimate. The API recomputes the fare authoritatively
/// at booking from this same chart, so the displayed and charged prices agree
/// while the charge is never set by a client-supplied number.
///
/// All money is integer <b>pence</b> (fast, exact — no floating-point drift);
/// multipliers/percentages are decimals.
/// </summary>
public class FareChart
{
    /// <summary>Monotonic version, bumped on every publish.</summary>
    public int Version { get; set; }

    public string Currency { get; set; } = "GBP";

    public DateTime UpdatedAtUtc { get; set; }

    public FareBase Base { get; set; } = new();
    public FareRates Rates { get; set; } = new();
    public List<FareTier> Tiers { get; set; } = new();
    public FareModifiers Modifiers { get; set; } = new();
    public PlatformConfig Platform { get; set; } = new();
}

/// <summary>Flat components applied once per trip.</summary>
public class FareBase
{
    /// <summary>Fixed booking fee added to every fare.</summary>
    public int BookingFeePence { get; set; }

    /// <summary>Floor — the fare is never charged below this.</summary>
    public int MinimumFarePence { get; set; }
}

/// <summary>Metered rates.</summary>
public class FareRates
{
    public int PerMilePence { get; set; }
    public int PerMinutePence { get; set; }
}

/// <summary>One bookable tier (Economy / Comfort / XL / Premium).</summary>
public class FareTier
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "car";

    /// <summary>Per-tier fixed component added before the multiplier.</summary>
    public int BaseFarePence { get; set; }

    /// <summary>Scales the whole metered subtotal (Economy = 1.0).</summary>
    public decimal Multiplier { get; set; } = 1m;

    public int Capacity { get; set; } = 4;

    /// <summary>Typical pickup ETA shown in the tier list, in minutes.</summary>
    public int EtaMinutes { get; set; }
}

/// <summary>
/// Dynamic price modifiers. Multiplicative ones (rush hour, busy area, outside
/// city) combine into one surge factor; zone surcharges are flat add-ons.
/// </summary>
public class FareModifiers
{
    public List<RushHourRule> RushHour { get; set; } = new();
    public List<ZoneSurcharge> Zones { get; set; } = new();
    public List<BusyArea> BusyAreas { get; set; } = new();
    public OutsideCityRule? OutsideCity { get; set; }
}

/// <summary>Time-of-day surge, e.g. weekday morning peak.</summary>
public class RushHourRule
{
    /// <summary>ISO weekdays it applies to: 1=Mon … 7=Sun. Empty = every day.</summary>
    public List<int> Days { get; set; } = new();

    /// <summary>Local start time, "HH:mm". Windows may wrap past midnight (From &gt; To).</summary>
    public string From { get; set; } = "00:00";
    public string To { get; set; } = "00:00";

    public decimal Multiplier { get; set; } = 1m;
}

/// <summary>Flat surcharge for a specific place (airport, station, event venue).</summary>
public class ZoneSurcharge
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int RadiusM { get; set; }
    public int SurchargePence { get; set; }
    public bool AppliesToPickup { get; set; } = true;
    public bool AppliesToDropoff { get; set; } = true;
}

/// <summary>A geographic surge bubble (high demand). Updated frequently by admin/ops.</summary>
public class BusyArea
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int RadiusM { get; set; }
    public decimal Multiplier { get; set; } = 1m;
}

/// <summary>Surge applied when the pickup falls outside the city boundary.</summary>
public class OutsideCityRule
{
    public double CityLat { get; set; }
    public double CityLng { get; set; }
    public int RadiusM { get; set; }
    public decimal Multiplier { get; set; } = 1m;
}

/// <summary>Platform economics.</summary>
public class PlatformConfig
{
    /// <summary>MAP CARS commission as a percent of the fare (e.g. 15).</summary>
    public decimal DriverFeePercent { get; set; }
}
