using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Technicians;

public class TechnicianProfileTests(PostgresFixture fixture) : TestBase(fixture)
{
    private static TechnicianInput Input(string code = "TEC-100", string color = "#1e88e5", params Guid[] skillIds) =>
        new(code, "+971501112233", color, 45.5m, new TimeOnly(8, 0), new TimeOnly(17, 0), skillIds);

    private async Task<Guid> GivenTechnician()
    {
        var user = await GivenUser(Role.Technician);
        return await NewDb().Technicians.Where(t => t.UserId == user.Id).Select(t => t.Id).SingleAsync();
    }

    private async Task<SkillDto> GivenSkill(string name) =>
        (await Resolve<CreateSkillHandler>().Handle(new CreateSkillCommand(new SkillInput(name)), default)).Value;

    private Task<Domain.Common.Result<TechnicianDto>> Update(Guid id, TechnicianInput input) =>
        Resolve<UpdateTechnicianHandler>().Handle(new UpdateTechnicianCommand(id, input), default);

    [Fact]
    public async Task Admin_sets_code_phone_color_cost_hours_and_skills()
    {
        var id = await GivenTechnician();
        var hvac = await GivenSkill("HVAC");
        var electrical = await GivenSkill("Electrical");

        var technician = (await Update(id, Input("tec-100", "#1e88e5", hvac.Id, electrical.Id))).Value;

        technician.EmployeeCode.ShouldBe("TEC-100");
        technician.Phone.ShouldBe("+971501112233");
        technician.Color.ShouldBe("#1E88E5");
        technician.HourlyCost.ShouldBe(45.5m);
        technician.WorkingHoursStart.ShouldBe(new TimeOnly(8, 0));
        technician.WorkingHoursEnd.ShouldBe(new TimeOnly(17, 0));
        technician.FullName.ShouldBe("Technician User");
        technician.Skills.Select(s => s.Name).ShouldBe(["Electrical", "HVAC"]);
    }

    [Fact]
    public async Task Skills_are_replaced_on_update()
    {
        var id = await GivenTechnician();
        var hvac = await GivenSkill("HVAC");
        var plumbing = await GivenSkill("Plumbing");
        await Update(id, Input(skillIds: hvac.Id));

        var technician = (await Update(id, Input(skillIds: plumbing.Id))).Value;

        technician.Skills.Select(s => s.Name).ShouldBe(["Plumbing"]);
        (await NewDb().Technicians.Include(t => t.Skills).SingleAsync(t => t.Id == id)).Skills.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Employee_code_is_unique_ignoring_case()
    {
        var first = await GivenTechnician();
        var second = await GivenTechnician();
        await Update(first, Input("TEC-777"));

        (await Update(second, Input("tec-777"))).Error.ShouldBe(TechnicianErrors.EmployeeCodeTaken);
    }

    [Fact]
    public async Task Keeping_the_same_code_is_allowed()
    {
        var id = await GivenTechnician();
        await Update(id, Input("TEC-500"));

        (await Update(id, Input("TEC-500"))).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("#1E88E5", true)]
    [InlineData("#abcdef", true)]
    [InlineData("1E88E5", false)]
    [InlineData("#1E88E", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData("blue", false)]
    public void Color_must_be_a_hex_value(string color, bool valid)
    {
        new TechnicianInputValidator().Validate(Input(color: color)).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Working_hours_must_end_after_they_start()
    {
        var input = Input() with { WorkingHoursStart = new TimeOnly(17, 0), WorkingHoursEnd = new TimeOnly(8, 0) };
        new TechnicianInputValidator().Validate(input).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Hourly_cost_cannot_be_negative()
    {
        new TechnicianInputValidator().Validate(Input() with { HourlyCost = -1 }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Unknown_skill_is_rejected()
    {
        var id = await GivenTechnician();

        (await Update(id, Input(skillIds: Guid.NewGuid()))).Error.ShouldBe(TechnicianErrors.UnknownSkill);
    }

    [Fact]
    public async Task Updating_a_missing_technician_returns_not_found()
    {
        (await Update(Guid.NewGuid(), Input())).Error.ShouldBe(TechnicianErrors.NotFound);
    }

    [Fact]
    public async Task Get_returns_the_profile_with_name_and_email()
    {
        var id = await GivenTechnician();

        var technician = (await Resolve<GetTechnicianHandler>().Handle(new GetTechnicianQuery(id), default)).Value;

        technician.Email.ShouldStartWith("technician-");
        technician.EmployeeCode.ShouldBe("TEC-001");
        technician.IsActive.ShouldBeTrue();
    }
}
