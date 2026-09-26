using FieldOps.Application.Features.Auth;
using FieldOps.Application.Features.Users;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Auth;

public class RefreshTests(PostgresFixture fixture) : TestBase(fixture)
{
    private Task<Domain.Common.Result<AuthResponse>> Refresh(string token) =>
        Resolve<RefreshHandler>().Handle(new RefreshCommand(token), default);

    [Fact]
    public async Task Valid_refresh_token_returns_a_new_pair_and_revokes_the_old_token()
    {
        var user = await GivenUser(Role.Dispatcher);
        var login = (await Login(user.Email)).Value;
        Fixture.Clock.Advance(TimeSpan.FromMinutes(20));

        var result = await Refresh(login.RefreshToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RefreshToken.ShouldNotBe(login.RefreshToken);
        result.Value.AccessTokenExpiresAt.ShouldBe(Fixture.Clock.GetUtcNow().AddMinutes(15));
        var tokens = await NewDb().RefreshTokens.Where(t => t.UserId == user.Id).OrderBy(t => t.CreatedAt).ToListAsync();
        tokens.Count.ShouldBe(2);
        tokens[0].RevokedAt.ShouldNotBeNull();
        tokens[0].ReplacedByTokenHash.ShouldBe(tokens[1].TokenHash);
        tokens[1].RevokedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Reusing_a_revoked_refresh_token_returns_unauthorized_and_revokes_the_whole_chain()
    {
        var user = await GivenUser(Role.Dispatcher);
        var first = (await Login(user.Email)).Value;
        var second = (await Refresh(first.RefreshToken)).Value;
        var third = (await Refresh(second.RefreshToken)).Value;

        var reuse = await Refresh(first.RefreshToken);

        reuse.Error.Code.ShouldBe("Auth.InvalidRefreshToken");
        (await NewDb().RefreshTokens.CountAsync(t => t.UserId == user.Id && t.RevokedAt == null)).ShouldBe(0);
        (await Refresh(third.RefreshToken)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var user = await GivenUser(Role.Dispatcher);
        var login = (await Login(user.Email)).Value;
        Fixture.Clock.Advance(TimeSpan.FromDays(7));

        var result = await Refresh(login.RefreshToken);

        result.Error.Code.ShouldBe("Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task Unknown_refresh_token_is_rejected()
    {
        var result = await Refresh("not-a-real-token");

        result.Error.Code.ShouldBe("Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task Logout_revokes_the_current_refresh_token()
    {
        var user = await GivenUser(Role.Technician);
        var login = (await Login(user.Email)).Value;
        Fixture.CurrentUser.SignInAs(user.Id, Role.Technician);

        var logout = await Resolve<LogoutHandler>().Handle(new LogoutCommand(login.RefreshToken), default);

        logout.IsSuccess.ShouldBeTrue();
        (await Refresh(login.RefreshToken)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Logout_cannot_revoke_another_users_token()
    {
        var victim = await GivenUser(Role.Dispatcher);
        var victimLogin = (await Login(victim.Email)).Value;
        var attacker = await GivenUser(Role.Technician);
        Fixture.CurrentUser.SignInAs(attacker.Id, Role.Technician);

        await Resolve<LogoutHandler>().Handle(new LogoutCommand(victimLogin.RefreshToken), default);

        (await Refresh(victimLogin.RefreshToken)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Deactivated_user_cannot_refresh()
    {
        var user = await GivenUser(Role.Dispatcher);
        var login = (await Login(user.Email)).Value;
        var admin = await GivenUser(Role.Admin);
        Fixture.CurrentUser.SignInAs(admin.Id, Role.Admin);
        await Resolve<SetUserActiveHandler>().Handle(new SetUserActiveCommand(user.Id, false), default);

        var result = await Refresh(login.RefreshToken);

        result.Error.Code.ShouldBe("Auth.InvalidRefreshToken");
    }
}
