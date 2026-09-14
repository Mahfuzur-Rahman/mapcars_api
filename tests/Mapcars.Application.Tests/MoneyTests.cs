using Mapcars.Application.Common;

namespace Mapcars.Application.Tests;

/// <summary>
/// Pounds ↔ pence.
///
/// This is the boundary where a payment provider gets told what to charge, so an
/// error here is money, not a display glitch.
/// </summary>
public class MoneyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.01, 1)]
    [InlineData(1.00, 100)]
    [InlineData(10.41, 1041)]   // a real fare from the seeded data
    [InlineData(12.50, 1250)]
    [InlineData(999999.99, 99999999)]
    public void ToPence_converts_exactly(decimal gbp, int pence)
        => Assert.Equal(pence, Money.ToPence(gbp));

    [Fact]
    public void Round_trips_losslessly_for_every_penny_in_a_plausible_fare_range()
    {
        // Exhaustive up to £500, which covers every fare this platform can
        // produce. Cheap, and it rules out the whole class at once rather than
        // spot-checking a few values.
        for (var pence = 0; pence <= 50_000; pence++)
        {
            Assert.Equal(pence, Money.ToPence(Money.FromPence(pence)));
        }
    }

    [Fact]
    public void Rounds_a_half_penny_away_from_zero_not_to_even()
    {
        // .NET rounds to even by default, so 0.125 would become 0.12. Money
        // convention is away from zero. This value cannot arise from our own
        // data (every amount originated as pence) — the guard is against a
        // corrupt or hand-edited row, and the direction should not quietly
        // favour either side.
        Assert.Equal(13, Money.ToPence(0.125m));
        Assert.Equal(15, Money.ToPence(0.145m));
    }

    [Fact]
    public void SumToPence_adds_rounded_parts_rather_than_rounding_a_sum()
    {
        // Identical today, because fares and tips are both already exact pence.
        Assert.Equal(1341, Money.SumToPence(10.41m, 3.00m));

        // The reason it is written this way: the moment a component is not an
        // exact penny — a percentage discount, say — rounding the sum and
        // summing the rounded parts diverge, and the charge stops matching what
        // the receipt itemises line by line.
        var summedThenRounded = Money.ToPence(0.005m + 0.005m);   // 0.01 -> 1
        var roundedThenSummed = Money.SumToPence(0.005m, 0.005m); // 1 + 1 -> 2
        Assert.Equal(1, summedThenRounded);
        Assert.Equal(2, roundedThenSummed);
    }

    [Fact]
    public void SumToPence_of_nothing_is_zero()
        => Assert.Equal(0, Money.SumToPence());

    [Theory]
    [InlineData(0, "£0.00")]
    [InlineData(5, "£0.05")]
    [InlineData(1041, "£10.41")]
    [InlineData(100000, "£1000.00")]
    public void Format_is_two_decimal_places(int pence, string expected)
        => Assert.Equal(expected, Money.Format(pence));
}
