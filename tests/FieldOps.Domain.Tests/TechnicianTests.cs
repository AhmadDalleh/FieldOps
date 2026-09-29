using FieldOps.Domain.Technicians;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class TechnicianTests
{
    private static Technician NewTechnician() => Technician.Create(Guid.CreateVersion7(), "TEC-001", null);

    private static TechnicianProfile Profile(string color = "#1e88e5", decimal cost = 40, int start = 8, int end = 17) =>
        new("TEC-001", "+971500000000", color, cost, new TimeOnly(start, 0), new TimeOnly(end, 0));

    [Theory]
    [InlineData("#1E88E5", true)]
    [InlineData("#abcdef", true)]
    [InlineData("1E88E5", false)]
    [InlineData("#1E88E", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData("red", false)]
    public void Color_must_be_a_six_digit_hex_value(string color, bool valid)
    {
        NewTechnician().UpdateProfile(Profile(color)).IsSuccess.ShouldBe(valid);
    }

    [Fact]
    public void Color_is_stored_in_upper_case()
    {
        var technician = NewTechnician();

        technician.UpdateProfile(Profile("#abcdef"));

        technician.Color.ShouldBe("#ABCDEF");
    }

    [Fact]
    public void Working_hours_must_end_after_they_start()
    {
        NewTechnician().UpdateProfile(Profile(start: 17, end: 8)).Error.Code.ShouldBe("Technician.InvalidWorkingHours");
    }

    [Fact]
    public void Hourly_cost_cannot_be_negative()
    {
        NewTechnician().UpdateProfile(Profile(cost: -1)).Error.Code.ShouldBe("Technician.InvalidHourlyCost");
    }

    [Fact]
    public void Setting_skills_adds_new_ones_and_removes_missing_ones()
    {
        var technician = NewTechnician();
        Guid hvac = Guid.CreateVersion7(), electrical = Guid.CreateVersion7(), plumbing = Guid.CreateVersion7();
        technician.SetSkills([hvac, electrical]);

        technician.SetSkills([electrical, plumbing, plumbing]);

        technician.Skills.Select(s => s.SkillId).ShouldBe([electrical, plumbing], ignoreOrder: true);
    }
}
