using FieldOps.Application.Features.Auth;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Identity;
using Shouldly;

namespace FieldOps.Application.Tests.Auth;

public class ChangePasswordTests(PostgresFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task Changing_password_with_the_current_password_lets_the_user_log_in_with_the_new_one()
    {
        var user = await GivenUser(Role.Technician);
        Fixture.CurrentUser.SignInAs(user.Id, Role.Technician);

        var result = await Resolve<ChangePasswordHandler>().Handle(new ChangePasswordCommand(Password, "NewPass456"), default);

        result.IsSuccess.ShouldBeTrue();
        (await Login(user.Email, "NewPass456")).IsSuccess.ShouldBeTrue();
        (await Login(user.Email, Password)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Wrong_current_password_is_rejected()
    {
        var user = await GivenUser(Role.Technician);
        Fixture.CurrentUser.SignInAs(user.Id, Role.Technician);

        var result = await Resolve<ChangePasswordHandler>().Handle(new ChangePasswordCommand("Wrong123!", "NewPass456"), default);

        result.IsFailure.ShouldBeTrue();
        (await Login(user.Email, Password)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Short1A", false)]
    [InlineData("nouppercase1", false)]
    [InlineData("NoDigitsHere", false)]
    [InlineData("GoodPass1", true)]
    public void New_password_must_have_8_characters_a_digit_and_an_uppercase_letter(string password, bool valid)
    {
        var result = new ChangePasswordValidator().Validate(new ChangePasswordCommand("Current1A", password));

        result.IsValid.ShouldBe(valid);
    }
}
