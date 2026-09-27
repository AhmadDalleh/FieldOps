using System.Net;
using System.Net.Http.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Features.Technicians;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Technicians;
using Shouldly;

namespace FieldOps.Api.Tests;

public class TechnicianApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static object Profile(string code = "TEC-100", string color = "#1E88E5", params Guid[] skillIds) => new
    {
        employeeCode = code, phone = "+971501112233", color, hourlyCost = 40,
        workingHoursStart = "08:00:00", workingHoursEnd = "17:00:00", skillIds,
    };

    private static object Leave(Guid? technicianId = null) => new
    {
        startsAt = "2026-12-01T04:00:00Z", endsAt = "2026-12-01T13:00:00Z", reason = "Leave", technicianId,
    };

    private async Task<(HttpClient Client, Guid TechnicianId)> GivenTechnician(HttpClient admin)
    {
        var user = await Factory.CreateUserAsync(Role.Technician);
        var list = await admin.GetFromJsonAsync<List<TechnicianAvailabilityDto>>("/api/technicians", Json.Options);
        return (await Factory.LoginAs(user), list!.Single(r => r.Technician.UserId == user.Id).Technician.Id);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;

    [Fact]
    public async Task Admin_manages_skills_and_technician_profiles()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        var (_, technicianId) = await GivenTechnician(admin);

        var created = await admin.PostAsJsonAsync("/api/skills", new { name = "HVAC" });
        var skill = await Read<SkillDto>(created);
        var renamed = await admin.PutAsJsonAsync($"/api/skills/{skill.Id}", new { name = "HVAC & Refrigeration" });
        var updated = await admin.PutAsJsonAsync($"/api/technicians/{technicianId}", Profile(skillIds: skill.Id));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var technician = await admin.GetFromJsonAsync<TechnicianDto>($"/api/technicians/{technicianId}", Json.Options);
        technician!.Skills.ShouldHaveSingleItem().Name.ShouldBe("HVAC & Refrigeration");
        technician.WorkingHoursStart.ShouldBe(new TimeOnly(8, 0));
    }

    [Fact]
    public async Task Duplicate_skill_name_returns_409()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        await admin.PostAsJsonAsync("/api/skills", new { name = "HVAC" });

        var response = await admin.PostAsJsonAsync("/api/skills", new { name = "hvac" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Skill.NameTaken");
    }

    [Fact]
    public async Task Duplicate_employee_code_returns_409_and_bad_color_returns_400()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        var (_, first) = await GivenTechnician(admin);
        var (_, second) = await GivenTechnician(admin);
        await admin.PutAsJsonAsync($"/api/technicians/{first}", Profile("TEC-9"));

        (await admin.PutAsJsonAsync($"/api/technicians/{second}", Profile("TEC-9"))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.PutAsJsonAsync($"/api/technicians/{second}", Profile(color: "blue"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dispatcher_can_view_but_not_change_technicians_or_skills()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        var (_, technicianId) = await GivenTechnician(admin);
        var skill = await Read<SkillDto>(await admin.PostAsJsonAsync("/api/skills", new { name = "HVAC" }));
        var dispatcher = await Factory.CreateClientAs(Role.Dispatcher);

        (await dispatcher.GetAsync("/api/technicians?date=2026-12-01")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await dispatcher.GetAsync($"/api/technicians/{technicianId}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await dispatcher.GetAsync("/api/skills")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await dispatcher.PutAsJsonAsync($"/api/technicians/{technicianId}", Profile())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await dispatcher.PostAsJsonAsync("/api/skills", new { name = "X" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await dispatcher.PutAsJsonAsync($"/api/skills/{skill.Id}", new { name = "X" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Technician_cannot_reach_technician_or_skill_endpoints()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        var (tech, technicianId) = await GivenTechnician(admin);

        var responses = new[]
        {
            await tech.GetAsync("/api/technicians"),
            await tech.GetAsync($"/api/technicians/{technicianId}"),
            await tech.PutAsJsonAsync($"/api/technicians/{technicianId}", Profile()),
            await tech.GetAsync("/api/skills"),
            await tech.PostAsJsonAsync("/api/skills", new { name = "X" }),
        };

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var anonymous = Factory.CreateClient();

        (await anonymous.GetAsync("/api/technicians")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/time-off")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Technician_requests_time_off_and_office_approves_it()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        var (tech, technicianId) = await GivenTechnician(admin);
        var dispatcher = await Factory.CreateClientAs(Role.Dispatcher);

        var requested = await tech.PostAsJsonAsync("/api/time-off", Leave());
        var request = await Read<TimeOffDto>(requested);
        var pending = await dispatcher.GetFromJsonAsync<List<TimeOffDto>>("/api/time-off?status=Pending", Json.Options);
        var approved = await dispatcher.PostAsync($"/api/time-off/{request.Id}/approve", null);
        var again = await dispatcher.PostAsync($"/api/time-off/{request.Id}/reject", null);

        requested.StatusCode.ShouldBe(HttpStatusCode.Created);
        request.TechnicianId.ShouldBe(technicianId);
        pending!.ShouldHaveSingleItem().Id.ShouldBe(request.Id);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Read<TimeOffDecision>(approved)).TimeOff.Status.ShouldBe(TimeOffStatus.Approved);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var day = await dispatcher.GetFromJsonAsync<List<TechnicianAvailabilityDto>>("/api/technicians?date=2026-12-01", Json.Options);
        day!.Single(r => r.Technician.Id == technicianId).IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task Technician_cannot_approve_or_request_for_others()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        var (tech, _) = await GivenTechnician(admin);
        var (_, other) = await GivenTechnician(admin);
        var request = await Read<TimeOffDto>(await tech.PostAsJsonAsync("/api/time-off", Leave()));

        (await tech.PostAsync($"/api/time-off/{request.Id}/approve", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await tech.PostAsync($"/api/time-off/{request.Id}/reject", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await tech.PostAsJsonAsync("/api/time-off", Leave(other))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Technician_lists_only_their_own_time_off()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        var (tech, mine) = await GivenTechnician(admin);
        var (_, other) = await GivenTechnician(admin);
        await admin.PostAsJsonAsync("/api/time-off", Leave(other));
        await tech.PostAsJsonAsync("/api/time-off", Leave());

        var list = await tech.GetFromJsonAsync<List<TimeOffDto>>($"/api/time-off?technicianId={other}", Json.Options);

        list!.ShouldHaveSingleItem().TechnicianId.ShouldBe(mine);
    }

    [Fact]
    public async Task Time_off_ending_before_it_starts_returns_400()
    {
        var tech = await Factory.CreateClientAs(Role.Technician);

        var response = await tech.PostAsJsonAsync("/api/time-off",
            new { startsAt = "2026-12-01T13:00:00Z", endsAt = "2026-12-01T04:00:00Z" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
