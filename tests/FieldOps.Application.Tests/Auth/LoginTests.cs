using System.IdentityModel.Tokens.Jwt;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Auth;

public class LoginTests(PostgresFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task Valid_credentials_return_a_15_minute_access_token_and_a_7_day_refresh_token()
    {
        var user = await GivenUser(Role.Dispatcher);

        var result = await Login(user.Email);

        result.IsSuccess.ShouldBeTrue();
        var now = Fixture.Clock.GetUtcNow();
        result.Value.AccessTokenExpiresAt.ShouldBe(now.AddMinutes(15));
        result.Value.RefreshTokenExpiresAt.ShouldBe(now.AddDays(7));
        result.Value.User.Role.ShouldBe(Role.Dispatcher);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.AccessToken);
        jwt.Subject.ShouldBe(user.Id.ToString());
        jwt.Claims.Single(c => c.Type == "role").Value.ShouldBe("Dispatcher");
    }

    [Fact]
    public async Task Refresh_token_is_stored_only_as_a_hash()
    {
        var user = await GivenUser(Role.Admin);

        var result = await Login(user.Email);

        var stored = await NewDb().RefreshTokens.SingleAsync(t => t.UserId == user.Id);
        stored.TokenHash.ShouldNotBe(result.Value.RefreshToken);
    }

    [Fact]
    public async Task Technician_token_carries_the_technician_id()
    {
        var user = await GivenUser(Role.Technician);
        var technician = await NewDb().Technicians.SingleAsync(t => t.UserId == user.Id);

        var result = await Login(user.Email);

        result.Value.User.TechnicianId.ShouldBe(technician.Id);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.AccessToken);
        jwt.Claims.Single(c => c.Type == "technician_id").Value.ShouldBe(technician.Id.ToString());
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_return_the_same_generic_unauthorized_error()
    {
        var user = await GivenUser(Role.Admin);

        var wrongPassword = await Login(user.Email, "Wrong123!");
        var unknownEmail = await Login("nobody@fieldops.test");

        wrongPassword.Error.Type.ShouldBe(ErrorType.Unauthorized);
        wrongPassword.Error.ShouldBe(unknownEmail.Error);
    }

    [Fact]
    public async Task Inactive_user_cannot_log_in()
    {
        var user = await GivenUser(Role.Dispatcher);
        var admin = await GivenUser(Role.Admin);
        Fixture.CurrentUser.SignInAs(admin.Id, Role.Admin);
        await Resolve<Features.Users.SetUserActiveHandler>().Handle(new(user.Id, false), default);

        var result = await Login(user.Email);

        result.Error.Code.ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Five_failed_attempts_lock_the_account_even_for_the_right_password()
    {
        var user = await GivenUser(Role.Dispatcher);

        for (var i = 0; i < 5; i++) await Login(user.Email, "Wrong123!");
        var result = await Login(user.Email);

        result.IsFailure.ShouldBeTrue();
        var stored = await NewDb().Users.SingleAsync(u => u.Id == user.Id);
        stored.LockoutEnd.ShouldNotBeNull();
        stored.LockoutEnd.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(14));
    }

    [Fact]
    public async Task Four_failed_attempts_do_not_lock_the_account()
    {
        var user = await GivenUser(Role.Dispatcher);

        for (var i = 0; i < 4; i++) await Login(user.Email, "Wrong123!");
        var result = await Login(user.Email);

        result.IsSuccess.ShouldBeTrue();
    }
}
