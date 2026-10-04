namespace Mapcars.Application.Pricing;

/// <summary>
/// One tier priced for a booking, together with what it was priced ON: the
/// route after clamping and the version of the chart that did the pricing.
///
/// <para>
/// The trip row snapshots all three so its fare is auditable. Snapshotting the
/// client's raw route instead would record numbers the fare was never computed
/// from, and reading the chart version separately could name a chart published
/// in between.
/// </para>
/// </summary>
public record PricedTier(
    FareBreakdown Fare,
    double DistanceMiles,
    double DurationMinutes,
    int FareChartVersion);
