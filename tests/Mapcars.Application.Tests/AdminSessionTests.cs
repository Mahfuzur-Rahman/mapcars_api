using Mapcars.Application.Admins.Services;
using Mapcars.Application.Auth.Services;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mapcars.Application.Tests;

/// <summary>
/// Admin login refuses a disabled account; these pin that the two places which
/// mint an admin JWT *without* a password — token refresh and "who am I" —
/// refuse it too. Otherwise disabling an admin only bites when their 30-day
/// refresh token finally runs out.
/// </summary>
public class AdminSessionTests
{
    private static Admin NewAdmin() => new() { Id = Guid.NewGuid(), Email = "ops@mapcars.uk", FullName = "Ops", RoleId = 2, IsActive = true };

    private sealed record Rig(RefreshTokenService Service, InMemoryRefreshTokenRepo Tokens, FakeUnitOfWork Uow);

    private static Rig RefreshRig(Admin admin)
    {
        var tokens = new InMemoryRefreshTokenRepo();
        var uow = new FakeUnitOfWork();
        // Customer/driver stores are never reached for an admin token.
        var service = new RefreshTokenService(tokens, null!, null!, new FakeAdminRepo(admin), new FakeJwt(), uow);
        return new Rig(service, tokens, uow);
    }

    [Fact]
    public async Task An_active_admin_refreshes_and_rotates()
    {
        var admin = NewAdmin();
        var rig = RefreshRig(admin);
        var raw = await rig.Service.IssueAsync(admin.Id, UserTypes.Admin);

        var response = await rig.Service.RefreshAsync(raw);

        Assert.Equal($"admin-jwt:{admin.Id}", response.Token);
        Assert.NotEqual(raw, response.RefreshToken);
        Assert.Equal(2, rig.Tokens.Rows.Count);
        Assert.NotNull(rig.Tokens.Rows[0].RevokedAtUtc);
    }

    [Fact]
    public async Task A_disabled_admin_is_refused_exactly_like_a_dead_token()
    {
        var admin = NewAdmin();
        var rig = RefreshRig(admin);
        var raw = await rig.Service.IssueAsync(admin.Id, UserTypes.Admin);
        var deadToken = await Assert.ThrowsAsync<UnauthorizedException>(() => rig.Service.RefreshAsync("never-issued"));

        admin.IsActive = false;
        var refused = await Assert.ThrowsAsync<UnauthorizedException>(() => rig.Service.RefreshAsync(raw));

        // Same exception, same message → same 401 problem body as an unknown token.
        Assert.Equal(deadToken.Message, refused.Message);
    }

    [Fact]
    public async Task A_disabled_admins_presented_token_is_revoked_and_not_replaced()
    {
        var admin = NewAdmin();
        var rig = RefreshRig(admin);
        var raw = await rig.Service.IssueAsync(admin.Id, UserTypes.Admin);
        var savesBefore = rig.Uow.Saves;

        admin.IsActive = false;
        await Assert.ThrowsAsync<UnauthorizedException>(() => rig.Service.RefreshAsync(raw));

        var row = Assert.Single(rig.Tokens.Rows);       // no successor minted
        Assert.NotNull(row.RevokedAtUtc);                // and the presented one is dead
        Assert.True(rig.Uow.Saves > savesBefore);        // ...persistently
    }

    [Fact]
    public async Task Who_am_i_will_not_mint_a_token_for_a_disabled_admin()
    {
        // GET /admin/auth/me returns a fresh admin JWT on every portal page load.
        // Without this check a disabled admin holding a live access token could
        // renew it there forever, never touching the refresh path at all.
        var admin = NewAdmin();
        var service = new AdminAuthService(
            new FakeAdminRepo(admin), new FakeJwt(), new SpyRefreshTokens(), null!, null!,
            NullLogger<AdminAuthService>.Instance, new FakeUnitOfWork());

        admin.IsActive = false;

        await Assert.ThrowsAsync<UnauthorizedException>(() => service.GetCurrentAdminAsync(admin.Id));
    }

    [Fact]
    public async Task Who_am_i_still_works_for_an_active_admin()
    {
        var admin = NewAdmin();
        var service = new AdminAuthService(
            new FakeAdminRepo(admin), new FakeJwt(), new SpyRefreshTokens(), null!, null!,
            NullLogger<AdminAuthService>.Instance, new FakeUnitOfWork());

        var response = await service.GetCurrentAdminAsync(admin.Id);

        Assert.Equal($"admin-jwt:{admin.Id}", response.Token);
    }
}
