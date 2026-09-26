namespace Mapcars.Application.Common;

/// <summary>
/// GBP ↔ integer pence.
///
/// <para>
/// This codebase has two money representations, split by concern and both
/// deliberate: the fare chart and its calculator work in <b>integer pence</b>
/// (<c>FareChart</c> says so in its header — exact, no floating-point drift),
/// while <c>Trip</c> persists <b>decimal pounds</b> in <c>NUMERIC(10,2)</c>.
/// Payment providers speak integer minor units.
/// </para>
///
/// <para>
/// So pence is the truth for anything that reaches a provider or a driver
/// statement, and this is the single place either direction is converted.
/// </para>
/// </summary>
public static class Money
{
    /// <summary>
    /// Pounds → pence.
    ///
    /// <para>
    /// The round trip is lossless <i>by construction</i>, not by luck: every
    /// value in Trip's money columns originated as pence
    /// (<c>TripService</c> divides by 100m when snapshotting the fare) and is
    /// stored as <c>NUMERIC(10,2)</c>, so <c>gbp * 100</c> is always an exact
    /// integer and no midpoint case can arise.
    /// </para>
    ///
    /// <para>
    /// The rounding mode is therefore a guard against corrupt data rather than a
    /// business rule — but it is <c>AwayFromZero</c> because that is the
    /// conventional choice for money, and a half-penny should not quietly round
    /// in the customer's favour just because .NET defaults to banker's rounding.
    /// </para>
    /// </summary>
    public static int ToPence(decimal gbp)
        => (int)decimal.Round(gbp * 100m, 0, MidpointRounding.AwayFromZero);

    public static decimal FromPence(int pence) => pence / 100m;

    /// <summary>
    /// Sum several pound amounts as pence.
    ///
    /// <para>
    /// Converts each component and adds in pence, rather than adding decimals and
    /// rounding once. Identical today, but the moment a discount or promotion
    /// lands, rounding a sum instead of summing rounded parts puts a penny
    /// between what is charged and what the receipt itemises — the single most
    /// common money bug in this shape.
    /// </para>
    /// </summary>
    public static int SumToPence(params decimal[] amounts)
    {
        var total = 0;
        foreach (var a in amounts) total += ToPence(a);
        return total;
    }

    /// <summary>Display only. Never use this to build an amount for a provider.</summary>
    public static string Format(int pence) => $"£{FromPence(pence):0.00}";
}
