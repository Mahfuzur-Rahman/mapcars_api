using Mapcars.Application.Common.Phones;
using Mapcars.Application.Customers.Services;
using Mapcars.Application.Drivers.Services;
using Mapcars.Domain.Exceptions;

namespace Mapcars.Application.Tests;

/// <summary>
/// The services must refuse a non-UK-mobile BEFORE any code is created or any
/// SMS is paid for. Every collaborator except the policy is null here, so
/// reaching one fails the test.
/// </summary>
public class PhoneSignInServiceTests
{
    [Theory]
    [InlineData("020 7946 0018")]
    [InlineData("+33 6 12 34 56 78")]
    public async Task A_customer_send_to_a_non_UK_mobile_is_refused_before_anything_is_sent(string phone)
    {
        var service = new CustomerAuthService(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, PhoneLoginPolicy.Default);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.SendPhoneOtpAsync(phone));
        Assert.Equal("Enter a valid UK mobile number.", ex.Message);
    }

    [Theory]
    [InlineData("020 7946 0018")]
    [InlineData("+33 6 12 34 56 78")]
    public async Task A_driver_send_to_a_non_UK_mobile_is_refused_before_anything_is_sent(string phone)
    {
        var service = new DriverAuthService(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, PhoneLoginPolicy.Default);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.SendPhoneOtpAsync(phone));
        Assert.Equal("Enter a valid UK mobile number.", ex.Message);
    }
}
