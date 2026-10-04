using System.Linq.Expressions;
using Mapcars.Application.Admins.Dtos;
using Mapcars.Application.Admins.Services;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.DriverReview.Services;
using Mapcars.Application.Notifications.Dtos;
using Mapcars.Application.Notifications.Interfaces;
using Mapcars.Application.Vehicles.Interfaces;
using Mapcars.Domain.Entities;
using Mapcars.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mapcars.Application.Tests;

/// <summary>
/// Email bodies are HTML: the provider sends them as the message body and the
/// admin Email page renders the stored copy. A driver chooses their own name, so
/// a name is markup unless it is encoded on the way in.
/// </summary>
public class EmailEncodingTests
{
    private const string HostileName = "<img src=x onerror=alert(1)>";
    private const string EncodedName = "&lt;img src=x onerror=alert(1)&gt;";

    [Fact]
    public async Task A_drivers_name_and_the_admin_note_are_encoded_in_the_tier_appeal_email()
    {
        var mail = new CaptureEmail();
        var appeal = new VehicleTierAppeal
        {
            Driver = new Driver { Email = "driver@example.com", FullName = HostileName },
            CurrentTier = "economy",
            RequestedTier = "premium",
            Reason = "New car",
        };
        appeal.DriverId = appeal.Driver.Id;

        // Only the appeal store, mailer, push and unit of work are reached on a rejection.
        var service = new DriverReviewService(
            null!, null!, null!, new FakeAppealRepo(appeal), null!, null!, mail, new NoopPush(), new FakeUnitOfWork(),
            NullLogger<DriverReviewService>.Instance);

        await service.ReviewTierAppealAsync(appeal.Id, Guid.NewGuid(), TierAppealStatus.Rejected, "<b>photos</b> & more");

        var body = Assert.Single(mail.Sent).Body;
        Assert.DoesNotContain("<img", body);
        Assert.Contains(EncodedName, body);
        Assert.DoesNotContain("<b>", body);
        Assert.Contains("&lt;b&gt;photos&lt;/b&gt; &amp; more", body);
    }

    [Fact]
    public async Task A_new_admins_name_is_encoded_in_the_welcome_email()
    {
        var mail = new CaptureEmail();
        var service = new AdminAuthService(
            new FakeAdminRepo(new Admin()), null!, null!, new FakeHasher(), mail,
            NullLogger<AdminAuthService>.Instance, new FakeUnitOfWork());

        await service.RegisterAsync(
            new CreateAdminRequest { Email = "new@mapcars.uk", Password = "irrelevant", FullName = HostileName, RoleId = 2 },
            createdBy: Guid.NewGuid());

        var body = Assert.Single(mail.Sent).Body;
        Assert.DoesNotContain("<img", body);
        Assert.Contains(EncodedName, body);
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash";
        public bool Verify(string password, string hash) => throw new NotSupportedException();
    }

    private sealed class NoopPush : IPushService
    {
        public Task NotifyUserAsync(string userType, Guid userId, PushMessage message, CancellationToken ct = default) => Task.CompletedTask;
        public Task RegisterAsync(string userType, Guid userId, RegisterDeviceRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UnregisterAsync(string token, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeAppealRepo(VehicleTierAppeal appeal) : IVehicleTierAppealRepository
    {
        public Task<VehicleTierAppeal?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(id == appeal.Id ? appeal : null);
        public void Update(VehicleTierAppeal entity) { }

        public Task<IReadOnlyList<VehicleTierAppeal>> ListForDriverAsync(Guid driverId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<VehicleTierAppeal?> GetActivePendingForDriverAsync(Guid driverId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VehicleTierAppeal>> ListAllAsync(TierAppealStatus? status = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<VehicleTierAppeal?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VehicleTierAppeal>> ListAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<VehicleTierAppeal>> FindAsync(Expression<Func<VehicleTierAppeal, bool>> predicate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(VehicleTierAppeal entity, CancellationToken ct = default) => throw new NotSupportedException();
        public void Remove(VehicleTierAppeal entity) => throw new NotSupportedException();
    }
}
