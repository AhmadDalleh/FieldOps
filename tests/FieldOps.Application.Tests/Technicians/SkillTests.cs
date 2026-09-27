using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Technicians;
using Shouldly;

namespace FieldOps.Application.Tests.Technicians;

public class SkillTests(PostgresFixture fixture) : TestBase(fixture)
{
    private Task<Domain.Common.Result<SkillDto>> Create(string name) =>
        Resolve<CreateSkillHandler>().Handle(new CreateSkillCommand(new SkillInput(name)), default);

    private Task<Domain.Common.Result<SkillDto>> Rename(Guid id, string name) =>
        Resolve<RenameSkillHandler>().Handle(new RenameSkillCommand(id, new SkillInput(name)), default);

    [Fact]
    public async Task Skill_is_created_and_listed_by_name()
    {
        await Create("Plumbing");
        await Create("  HVAC ");

        var skills = await Resolve<ListSkillsHandler>().Handle(new ListSkillsQuery(), default);

        skills.Select(s => s.Name).ShouldBe(["HVAC", "Plumbing"]);
    }

    [Theory]
    [InlineData("HVAC")]
    [InlineData("hvac")]
    [InlineData(" Hvac ")]
    public async Task Skill_names_are_unique_ignoring_case(string duplicate)
    {
        await Create("HVAC");

        (await Create(duplicate)).Error.ShouldBe(TechnicianErrors.SkillNameTaken);
    }

    [Fact]
    public async Task Skill_can_be_renamed()
    {
        var skill = (await Create("Electric")).Value;

        (await Rename(skill.Id, "Electrical")).Value.Name.ShouldBe("Electrical");
    }

    [Fact]
    public async Task Renaming_only_the_case_of_a_skill_is_allowed()
    {
        var skill = (await Create("hvac")).Value;

        (await Rename(skill.Id, "HVAC")).Value.Name.ShouldBe("HVAC");
    }

    [Fact]
    public async Task Renaming_to_another_skills_name_is_rejected()
    {
        await Create("HVAC");
        var plumbing = (await Create("Plumbing")).Value;

        (await Rename(plumbing.Id, "hvac")).Error.ShouldBe(TechnicianErrors.SkillNameTaken);
    }

    [Fact]
    public async Task Renaming_a_missing_skill_returns_not_found()
    {
        (await Rename(Guid.NewGuid(), "X")).Error.ShouldBe(TechnicianErrors.SkillNotFound);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("HVAC", true)]
    public void Name_is_required(string name, bool valid)
    {
        new SkillInputValidator().Validate(new SkillInput(name)).IsValid.ShouldBe(valid);
    }
}
