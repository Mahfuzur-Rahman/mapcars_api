using Mapcars.Application.Auth.Services;
using Mapcars.Application.Common.Exceptions;
using Mapcars.Domain.Constants;
using Mapcars.Domain.Entities;

namespace Mapcars.Application.Tests;

/// <summary>
/// A rotated refresh token presented again is normally theft, and revokes every
/// session. The exception: the same client asking again moments later, because
/// the reply to its first rotation never arrived (a mobile network dropped it, or
/// two web requests renewed at once). Treating that as theft signed people out
/// for nothing. See the spec, §3.2.
/// </summary>
public class RefreshTokenGraceTests
{
    private static Admin NewAdmin() => new() { Id = Guid.NewGuid(), Email = "ops@mapcars.uk", FullName = "Ops", RoleId = 2, IsActive = true };

    private sealed record Rig(RefreshTokenService Service, InMemoryRefreshTokenRepo Tokens);

    // Admin tokens keep the rig small: the customer and driver stores are never reached.
    private static Rig NewRig(Admin admin)
    {
        var tokens = new InMemoryRefreshTokenRepo();
        var service = new RefreshTokenService(tokens, null!, null!, new FakeAdminRepo(admin), new FakeJwt(), new FakeUnitOfWork());
        return new Rig(service, tokens);
    }

    private static RefreshToken RowFor(Rig rig, int index) => rig.Tokens.Rows[index];

    [Fact]
    public async Task A_rotated_token_presented_again_within_the_grace_window_gets_a_fresh_token()
    {
        var admin = NewAdmin();
        var rig = NewRig(admin);
        var raw = await rig.Service.IssueAsync(admin.Id, UserTypes.Admin);
        var first = await rig.Service.RefreshAsync(raw);

        var retry = await rig.Service.RefreshAsync(raw);

        Assert.Equal($"admin-jwt:{admin.Id}", retry.Token);
        Assert.NotEqual(first.RefreshToken, retry.RefreshToken);
        Assert.Equal(3, rig.Tokens.Rows.Count);
        // Nothing was revoked: the first successor (never received) and the new one both still work.
        Assert.Null(RowFor(rig, 1).RevokedAtUtc);
        Assert.Null(RowFor(rig, 2).RevokedAtUtc);
    }

    [Fact]
    public async Task Re_presenting_after_the_grace_window_is_still_treated_as_theft()
    {
        var admin = NewAdmin();
        var rig = NewRig(admin);
        var raw = await rig.Service.IssueAsync(admin.Id, UserTypes.Admin);
        await rig.Service.RefreshAsync(raw);
        RowFor(rig, 0).RevokedAtUtc = DateTime.UtcNow - RefreshTokenService.RotationGrace - TimeSpan.FromSeconds(1);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => rig.Service.RefreshAsync(raw));

        Assert.Equal("Your session was ended for security. Please sign in again.", ex.Message);
        Assert.All(rig.Tokens.Rows, t => Assert.NotNull(t.RevokedAtUtc));
    }

    [Fact]
    public async Task Re_presenting_after_the_successor_was_used_is_theft()
    {
        var admin = NewAdmin();
        var rig = NewRig(admin);
        var raw = await rig.Service.IssueAsync(admin.Id, UserTypes.Admin);
        var first = await rig.Service.RefreshAsync(raw);
        await rig.Service.RefreshAsync(first.RefreshToken); // the client did get its reply, and used it

        await Assert.ThrowsAsync<UnauthorizedException>(() => rig.Service.RefreshAsync(raw));

        Assert.All(rig.Tokens.Rows, t => Assert.NotNull(t.RevokedAtUtc));
    }

    [Fact]
    public async Task A_logged_out_token_is_never_a_retry()
    {
        var admin = NewAdmin();
        var rig = NewRig(admin);
        var raw = await rig.Service.IssueAsync(admin.Id, UserTypes.Admin);
        await rig.Service.RevokeAsync(raw); // logout: revoked, but not by rotation

        await Assert.ThrowsAsync<UnauthorizedException>(() => rig.Service.RefreshAsync(raw));

        Assert.Single(rig.Tokens.Rows); // no successor minted
    }
}
