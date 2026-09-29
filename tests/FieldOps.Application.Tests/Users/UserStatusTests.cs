using FieldOps.Domain.Inventory;
using FieldOps.Application.Features.Users;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Users;

public class UserStatusTests(PostgresFixture fixture) : TestBase(fixture)
{
    private async Task<UserDto> SignInAsAdmin()
    {
        var admin = await GivenUser(Role.Admin);
        Fixture.CurrentUser.SignInAs(admin.Id, Role.Admin);
        return admin;
    }

    [Fact]
    public async Task Deactivating_a_user_blocks_login_and_revokes_their_refresh_tokens()
    {
        await SignInAsAdmin();
        var user = await GivenUser(Role.Technician);
        await Login(user.Email);
        await Login(user.Email);

        var result = await Resolve<SetUserActiveHandler>().Handle(new SetUserActiveCommand(user.Id, false), default);

        result.IsSuccess.ShouldBeTrue();
        (await Login(user.Email)).IsFailure.ShouldBeTrue();
        var db = NewDb();
        (await db.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.RevokedAt == null)).ShouldBe(0);
        (await db.Technicians.SingleAsync(t => t.UserId == user.Id)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivated_users_keep_their_records()
    {
        await SignInAsAdmin();
        var user = await GivenUser(Role.Technician);

        await Resolve<SetUserActiveHandler>().Handle(new SetUserActiveCommand(user.Id, false), default);

        var fetched = await Resolve<GetUserHandler>().Handle(new GetUserQuery(user.Id), default);
        fetched.Value.IsActive.ShouldBeFalse();
        (await NewDb().StockLocations.CountAsync(l => l.Type == StockLocationType.Van)).ShouldBe(1);
    }

    [Fact]
    public async Task Reactivating_a_user_lets_them_log_in_again()
    {
        await SignInAsAdmin();
        var user = await GivenUser(Role.Technician);
        await Resolve<SetUserActiveHandler>().Handle(new SetUserActiveCommand(user.Id, false), default);

        await Resolve<SetUserActiveHandler>().Handle(new SetUserActiveCommand(user.Id, true), default);

        (await Login(user.Email)).IsSuccess.ShouldBeTrue();
        (await NewDb().Technicians.SingleAsync(t => t.UserId == user.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Admin_cannot_deactivate_themselves()
    {
        var admin = await SignInAsAdmin();

        var result = await Resolve<SetUserActiveHandler>().Handle(new SetUserActiveCommand(admin.Id, false), default);

        result.Error.Code.ShouldBe("User.CannotDeactivateSelf");
    }

    [Fact]
    public async Task Unknown_user_returns_not_found()
    {
        await SignInAsAdmin();

        var result = await Resolve<SetUserActiveHandler>().Handle(new SetUserActiveCommand(Guid.CreateVersion7(), false), default);

        result.Error.Code.ShouldBe("User.NotFound");
    }

    [Fact]
    public async Task Changing_a_technician_to_dispatcher_deactivates_the_technician_row()
    {
        await SignInAsAdmin();
        var user = await GivenUser(Role.Technician);

        var result = await Resolve<UpdateUserHandler>().Handle(
            new UpdateUserCommand(user.Id, "Now Office", user.Email, null, Role.Dispatcher), default);

        result.Value.Role.ShouldBe(Role.Dispatcher);
        result.Value.FullName.ShouldBe("Now Office");
        (await NewDb().Technicians.SingleAsync(t => t.UserId == user.Id)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Changing_a_dispatcher_to_technician_provisions_the_technician_and_van()
    {
        await SignInAsAdmin();
        var user = await GivenUser(Role.Dispatcher);

        await Resolve<UpdateUserHandler>().Handle(new UpdateUserCommand(user.Id, "Field Person", user.Email, null, Role.Technician), default);

        var db = NewDb();
        var technician = await db.Technicians.SingleAsync(t => t.UserId == user.Id);
        (await db.StockLocations.SingleAsync(l => l.TechnicianId == technician.Id)).Name.ShouldBe("Van - Field Person");
    }

    [Fact]
    public async Task Updating_to_an_email_used_by_someone_else_returns_conflict()
    {
        await SignInAsAdmin();
        await GivenUser(Role.Dispatcher, "taken@fieldops.test");
        var user = await GivenUser(Role.Dispatcher);

        var result = await Resolve<UpdateUserHandler>().Handle(
            new UpdateUserCommand(user.Id, "Name", "taken@fieldops.test", null, Role.Dispatcher), default);

        result.Error.Code.ShouldBe("User.EmailTaken");
    }
}
