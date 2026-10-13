using Mapcars.Application.Common.Phones;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Tests;

/// <summary>
/// One person, one number. The apps used to send "+4407400…" while the web sent
/// "+447400…", so the same customer could end up with two accounts. Every way a
/// UK mobile can be typed must land on the same E.164 string, and only UK
/// mobiles may receive a sign-in code (spec §1.1).
/// </summary>
public class PhoneLoginPolicyTests
{
    private static readonly PhoneLoginPolicy Policy = PhoneLoginPolicy.Default;

    [Theory]
    [InlineData("07400 123456")]
    [InlineData("+44 7400 123456")]
    [InlineData("+4407400123456")]   // what the apps sent: "+44" glued to "07400…"
    [InlineData("447400123456")]
    [InlineData("(07400) 123-456")]
    [InlineData("7400123456")]       // typed next to the fixed +44 field
    public void Every_way_of_typing_a_UK_mobile_gives_the_same_number(string typed)
        => Assert.Equal("+447400123456", Policy.NormalizeLoginPhone(typed));

    [Theory]
    [InlineData("020 7946 0018")]          // UK landline
    [InlineData("+33 6 12 34 56 78")]      // French mobile
    [InlineData("12")]
    [InlineData("call me")]
    public void Anything_but_a_UK_mobile_is_refused(string typed)
    {
        var ex = Assert.Throws<DomainException>(() => Policy.NormalizeLoginPhone(typed));
        Assert.Equal("Enter a valid UK mobile number.", ex.Message);
    }

    [Fact]
    public void A_configured_test_number_is_accepted_however_it_is_typed()
    {
        var policy = new PhoneLoginPolicy(["GB"], 500, [new TestPhoneNumber("+447700900001", "246810")]);

        Assert.Equal("+447700900001", policy.NormalizeLoginPhone("07700 900001"));
        Assert.Equal("246810", policy.TestCodeFor("+447700900001"));
        Assert.Null(policy.TestCodeFor("+447400123456"));
    }

    [Theory]
    [InlineData("+447400123456", "246810")]   // a real mobile: would be a backdoor
    [InlineData("+447700901000", "246810")]   // just outside the reserved range
    [InlineData("+447700900001", "12345")]    // code not 6 digits
    [InlineData("+447700900001", "12345a")]
    public void A_bad_test_number_config_refuses_to_start(string number, string code)
        => Assert.Throws<InvalidOperationException>(
            () => new PhoneLoginPolicy(["GB"], 500, [new TestPhoneNumber(number, code)]));

    [Fact]
    public void An_empty_region_list_or_a_zero_cap_refuses_to_start()
    {
        Assert.Throws<InvalidOperationException>(() => new PhoneLoginPolicy([], 500, []));
        Assert.Throws<InvalidOperationException>(() => new PhoneLoginPolicy(["GB"], 0, []));
    }

    [Theory]
    [InlineData("+33 6 12 34 56 78", "+33612345678")]
    [InlineData("07400 123456", "+447400123456")]
    [InlineData("020 7946 0018", "+442079460018")]
    [InlineData("call me", "call me")]
    public void Emergency_contacts_are_formatted_when_they_parse_and_kept_otherwise(string typed, string stored)
        => Assert.Equal(stored, PhoneFormat.NormalizeAny(typed));
}
