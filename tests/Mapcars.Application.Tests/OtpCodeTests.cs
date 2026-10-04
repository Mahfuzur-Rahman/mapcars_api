using Mapcars.Application.Common;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Tests;

/// <summary>
/// The pure half of OTP checking: what a code looks like, how a guess is
/// compared, and the guard the attempt counter is bumped under.
///
/// The guard is evaluated by Postgres inside one conditional UPDATE — that is
/// what makes it atomic, and that part needs a real database to prove. These
/// tests pin the decision itself: which codes may still be guessed at.
/// </summary>
public class OtpCodeTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    private static VerificationCode Live(int attempts = 0) => new()
    {
        Id = Guid.NewGuid(),
        Code = "123456",
        ExpiresAt = Now.AddMinutes(3),
        AttemptCount = attempts,
    };

    private static bool MayAttempt(VerificationCode code) => OtpCode.MayAttempt(Now).Compile()(code);

    [Fact]
    public void Codes_are_six_digits_with_no_leading_zero()
    {
        // Same shape as the Random.Shared codes they replace: the code column is
        // VARCHAR(6), and a client that parses the code as a number must not
        // lose a leading zero.
        for (var i = 0; i < 10_000; i++)
        {
            var code = OtpCode.Generate();
            Assert.Equal(6, code.Length);
            Assert.All(code, c => Assert.InRange(c, '0', '9'));
            Assert.InRange(int.Parse(code), 100_000, 999_999);
        }
    }

    [Fact]
    public void Codes_are_not_repeated_in_a_small_sample()
    {
        // Not a randomness test — a guard against a constant or a reused seed.
        var codes = Enumerable.Range(0, 50).Select(_ => OtpCode.Generate()).ToHashSet();
        Assert.True(codes.Count > 40);
    }

    [Theory]
    [InlineData("123456", true)]
    [InlineData("123457", false)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("", false)]
    [InlineData(" 123456", false)]
    public void A_guess_matches_only_the_exact_code(string guess, bool expected)
        => Assert.Equal(expected, OtpCode.Matches("123456", guess));

    [Fact]
    public void A_null_guess_never_matches()
        => Assert.False(OtpCode.Matches("123456", null));

    [Fact]
    public void A_fresh_code_may_be_guessed_at()
        => Assert.True(MayAttempt(Live()));

    [Fact]
    public void The_last_allowed_guess_is_still_compared()
        => Assert.True(MayAttempt(Live(attempts: IOtpService.MaxAttempts - 1)));

    [Fact]
    public void A_code_at_the_cap_is_locked_even_for_the_right_guess()
        => Assert.False(MayAttempt(Live(attempts: IOtpService.MaxAttempts)));

    [Fact]
    public void A_used_code_cannot_be_guessed_at()
    {
        var code = Live();
        code.UsedAt = Now.AddSeconds(-1);
        Assert.False(MayAttempt(code));
    }

    [Fact]
    public void An_expired_code_cannot_be_guessed_at()
    {
        var code = Live();
        code.ExpiresAt = Now;
        Assert.False(MayAttempt(code));
    }

    [Fact]
    public void Exactly_MaxAttempts_guesses_are_ever_compared()
    {
        // Replays what the UPDATE does per request — bump only while the guard
        // holds — over a burst of wrong guesses followed by the right one. The
        // right code arriving after the cap must not get in.
        var code = Live();
        var compared = 0;
        var guesses = Enumerable.Repeat("000000", 20).Append(code.Code);

        var accepted = false;
        foreach (var guess in guesses)
        {
            if (!MayAttempt(code)) continue; // rows-affected 0: locked out
            code.AttemptCount++;
            compared++;
            if (OtpCode.Matches(code.Code, guess)) accepted = true;
        }

        Assert.Equal(IOtpService.MaxAttempts, compared);
        Assert.False(accepted);
    }
}
