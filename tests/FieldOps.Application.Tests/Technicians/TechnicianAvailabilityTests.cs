using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Features.Users;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Technicians;

public class TechnicianAvailabilityTests(PostgresFixture fixture) : TestBase(fixture)
{
    // The fixture clock is 2026-10-01 06:00 UTC, which is 10:00 on 1 October in Dubai.
    private static readonly DateOnly Today = new(2026, 10, 1);

    private async Task GivenTimeOff(Guid technicianId, DateTimeOffset startsAt, DateTimeOffset endsAt, bool? approve)
    {
        var db = NewDb();
        var timeOff = TimeOff.Request(technicianId, startsAt, endsAt, "Leave").Value;
        if (approve == true) timeOff.Approve();
        if (approve == false) timeOff.Reject();
        db.TimeOffs.Add(timeOff);
        await db.SaveChangesAsync();
    }

    private Task<IReadOnlyList<TechnicianAvailabilityDto>> List(DateOnly? date = null, bool includeInactive = false) =>
        Resolve<ListTechniciansHandler>().Handle(new ListTechniciansQuery(date, includeInactive), default);

    [Fact]
    public async Task Lists_technicians_with_skills_for_today_by_default()
    {
        var (_, technicianId) = await GivenTechnician();
        var skill = (await Resolve<CreateSkillHandler>().Handle(new CreateSkillCommand(new SkillInput("HVAC")), default)).Value;
        await Resolve<UpdateTechnicianHandler>().Handle(new UpdateTechnicianCommand(technicianId,
            new TechnicianInput("TEC-001", null, "#1E88E5", 40, new TimeOnly(8, 0), new TimeOnly(17, 0), [skill.Id])), default);

        var row = (await List()).ShouldHaveSingleItem();

        row.Date.ShouldBe(Today);
        row.Technician.Skills.ShouldHaveSingleItem().Name.ShouldBe("HVAC");
        row.JobCount.ShouldBe(0);
        row.TimeOff.ShouldBeEmpty();
        row.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Approved_time_off_that_overlaps_the_day_makes_the_technician_unavailable()
    {
        var (_, technicianId) = await GivenTechnician();
        // 1 Oct 13:00-15:00 Dubai.
        await GivenTimeOff(technicianId, new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), new(2026, 10, 1, 11, 0, 0, TimeSpan.Zero), approve: true);

        var row = (await List()).ShouldHaveSingleItem();

        row.IsAvailable.ShouldBeFalse();
        row.TimeOff.ShouldHaveSingleItem().Reason.ShouldBe("Leave");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task Only_approved_time_off_affects_availability(bool? approve)
    {
        var (_, technicianId) = await GivenTechnician();
        await GivenTimeOff(technicianId, new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), new(2026, 10, 1, 11, 0, 0, TimeSpan.Zero), approve);

        (await List()).ShouldHaveSingleItem().IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task The_day_follows_the_Dubai_calendar()
    {
        var (_, technicianId) = await GivenTechnician();
        // 30 Sep 21:00 UTC is already 1 Oct 01:00 in Dubai.
        await GivenTimeOff(technicianId, new(2026, 9, 30, 21, 0, 0, TimeSpan.Zero), new(2026, 9, 30, 22, 0, 0, TimeSpan.Zero), approve: true);

        (await List(Today)).Single().IsAvailable.ShouldBeFalse();
        (await List(Today.AddDays(-1))).Single().IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Time_off_ending_exactly_at_the_start_of_the_day_does_not_count()
    {
        var (_, technicianId) = await GivenTechnician();
        // Ends at 1 Oct 00:00 Dubai.
        await GivenTimeOff(technicianId, new(2026, 9, 30, 10, 0, 0, TimeSpan.Zero), new(2026, 9, 30, 20, 0, 0, TimeSpan.Zero), approve: true);

        (await List(Today)).Single().IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Multi_day_time_off_covers_every_day_in_between()
    {
        var (_, technicianId) = await GivenTechnician();
        await GivenTimeOff(technicianId, new(2026, 9, 28, 4, 0, 0, TimeSpan.Zero), new(2026, 10, 5, 13, 0, 0, TimeSpan.Zero), approve: true);

        (await List(Today)).Single().IsAvailable.ShouldBeFalse();
        (await List(new DateOnly(2026, 10, 6))).Single().IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Inactive_technicians_are_hidden_unless_asked_for()
    {
        var (userId, _) = await GivenTechnician();
        await GivenTechnician();
        Fixture.CurrentUser.SignInAs((await GivenUser(Role.Admin)).Id, Role.Admin);
        await Resolve<SetUserActiveHandler>().Handle(new SetUserActiveCommand(userId, false), default);

        (await List()).Count.ShouldBe(1);
        var all = await List(includeInactive: true);
        all.Count.ShouldBe(2);
        all.Single(r => !r.Technician.IsActive).IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task Job_count_is_the_number_of_jobs_scheduled_that_day()
    {
        var (_, technicianId) = await GivenTechnician();
        var (c, s) = await GivenCustomerAndSite();
        var today = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        foreach (var (start, cancel) in new[] { (today, false), (today.AddHours(3), false), (today.AddHours(5), true), (today.AddDays(1), false) })
        {
            var wo = await GivenWorkOrder(c, s);
            await Advance(wo.Id, (w, u, now) => w.Schedule(technicianId, start, start.AddHours(1), u, now));
            if (cancel) await Advance(wo.Id, (w, u, now) => w.Cancel("Dup", u, now));
        }

        (await List(Today)).Single().JobCount.ShouldBe(2);
        (await List(Today.AddDays(1))).Single().JobCount.ShouldBe(1);
    }
}
