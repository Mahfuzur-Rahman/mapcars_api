using Mapcars.Application.Common.Phones;

namespace Mapcars.Application.Tests;

/// <summary>
/// SMS costs money per message, and sign-in codes are the classic target of
/// SMS-pumping fraud. The guard caps sends per number and platform-wide
/// (spec §1.2). Refusals carry how long to wait, so the apps can count down.
/// </summary>
public class PhoneSendGuardTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private static SendDecision Decide(IEnumerable<TimeSpan> agoTimes, int platform = 0, int cap = 500)
        => PhoneSendGuard.Decide(Now, agoTimes.Select(a => Now - a).ToList(), platform, cap);

    [Fact]
    public void A_first_send_is_allowed()
        => Assert.False(Decide([]).Refused);

    [Fact]
    public void A_second_send_within_60_seconds_waits_out_the_remainder()
    {
        var d = Decide([TimeSpan.FromSeconds(20)]);

        Assert.Equal(SendRefusal.Cooldown, d.Refusal);
        Assert.Equal(40, d.RetryAfterSeconds);
    }

    [Fact]
    public void The_sixth_send_in_an_hour_waits_until_the_oldest_leaves_the_hour()
    {
        var d = Decide([TimeSpan.FromMinutes(50), TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(30),
                        TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(10)]);

        Assert.Equal(SendRefusal.Hourly, d.Refusal);
        Assert.Equal(10 * 60, d.RetryAfterSeconds);
    }

    [Fact]
    public void The_eleventh_send_in_a_day_waits_until_the_oldest_leaves_the_day()
    {
        var sends = Enumerable.Range(0, 10).Select(i => TimeSpan.FromHours(23 - i * 2)).ToList(); // 23h ago … 5h ago

        var d = Decide(sends);

        Assert.Equal(SendRefusal.Daily, d.Refusal);
        Assert.Equal(60 * 60, d.RetryAfterSeconds);
    }

    [Fact]
    public void The_platform_cap_refuses_everyone()
    {
        var d = Decide([], platform: 500, cap: 500);

        Assert.Equal(SendRefusal.PlatformCap, d.Refusal);
        Assert.True(d.RetryAfterSeconds > 0);
        Assert.Equal("We can't send codes right now. Please try again later, or sign in with email.", d.Message);
    }

    [Fact]
    public void Several_limits_at_once_wait_for_the_longest()
    {
        // Cooldown (40 s) and hourly (oldest at 59 min → 60 s) both apply.
        var d = Decide([TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(20),
                        TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(20)]);

        Assert.Equal(60, d.RetryAfterSeconds);
    }

    [Fact]
    public void A_per_number_refusal_says_how_long_to_wait()
        => Assert.Equal("Please wait 40 seconds before asking for another code.", Decide([TimeSpan.FromSeconds(20)]).Message);
}
