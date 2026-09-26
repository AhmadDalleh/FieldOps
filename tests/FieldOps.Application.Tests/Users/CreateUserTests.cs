using FieldOps.Application.Common;
using FieldOps.Application.Features.Users;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Users;

public class CreateUserTests(PostgresFixture fixture) : TestBase(fixture)
{
    [Fact]
    public async Task Admin_creates_a_user_with_name_email_phone_role_and_temporary_password()
    {
        var result = await Resolve<CreateUserHandler>().Handle(
            new CreateUserCommand("Sara Khan", "sara@fieldops.test", "+971501112233", Role.Dispatcher, "Temp1234"), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new UserDto(result.Value.Id, "sara@fieldops.test", "Sara Khan", "+971501112233", Role.Dispatcher, true));
        (await Login("sara@fieldops.test", "Temp1234")).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Creating_a_technician_also_creates_the_technician_row_and_a_van_stock_location()
    {
        var user = await GivenUser(Role.Technician);

        var db = NewDb();
        var technician = await db.Technicians.SingleAsync(t => t.UserId == user.Id);
        technician.EmployeeCode.ShouldBe("TEC-001");
        technician.IsActive.ShouldBeTrue();
        var van = await db.StockLocations.SingleAsync(l => l.TechnicianId == technician.Id);
        van.Type.ShouldBe(StockLocationType.Van);
        van.Name.ShouldBe("Van - Technician User");
    }

    [Fact]
    public async Task Technician_employee_codes_are_sequential()
    {
        await GivenUser(Role.Technician);
        var second = await GivenUser(Role.Technician);

        var technician = await NewDb().Technicians.SingleAsync(t => t.UserId == second.Id);
        technician.EmployeeCode.ShouldBe("TEC-002");
    }

    [Fact]
    public async Task Creating_an_office_user_creates_no_technician_row()
    {
        await GivenUser(Role.Dispatcher);

        (await NewDb().Technicians.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Duplicate_email_returns_conflict()
    {
        await GivenUser(Role.Dispatcher, "same@fieldops.test");

        var result = await Resolve<CreateUserHandler>().Handle(
            new CreateUserCommand("Other", "same@fieldops.test", null, Role.Admin, "Temp1234"), default);

        result.Error.Code.ShouldBe("User.EmailTaken");
    }

    [Fact]
    public async Task Audit_fields_record_who_created_the_technician()
    {
        var admin = await GivenUser(Role.Admin);
        Fixture.CurrentUser.SignInAs(admin.Id, Role.Admin);

        var user = await GivenUser(Role.Technician);

        var technician = await NewDb().Technicians.SingleAsync(t => t.UserId == user.Id);
        technician.CreatedBy.ShouldBe(admin.Id);
        technician.CreatedAt.ShouldBe(Fixture.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Users_can_be_searched_and_paged()
    {
        await GivenUser(Role.Admin, "alpha@fieldops.test");
        await GivenUser(Role.Dispatcher, "beta@fieldops.test");
        await GivenUser(Role.Technician, "gamma@fieldops.test");

        var page = await Resolve<ListUsersHandler>().Handle(new ListUsersQuery(new PageRequest(1, 2, null, "email")), default);
        var search = await Resolve<ListUsersHandler>().Handle(new ListUsersQuery(new PageRequest(Search: "BETA")), default);

        page.TotalCount.ShouldBe(3);
        page.Items.Select(u => u.Email).ShouldBe(["alpha@fieldops.test", "beta@fieldops.test"]);
        search.Items.Single().Role.ShouldBe(Role.Dispatcher);
    }
}
