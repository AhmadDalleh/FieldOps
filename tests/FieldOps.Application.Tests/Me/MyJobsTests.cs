using FieldOps.Application.Common;
using FieldOps.Application.Features.Me;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Application.Tests.Me;

public class MyJobsTests(PostgresFixture fixture) : TestBase(fixture)
{
    // The clock starts at 1 Oct 2026 06:00 UTC, 10:00 in Dubai.
    private async Task<Guid> Job((Guid UserId, Guid TechnicianId) tech, Guid c, Guid s, string title, TimeSpan startsIn)
    {
        var wo = await GivenWorkOrder(c, s, title: title);
        await Advance(wo.Id, (w, u, now) => w.Schedule(tech.TechnicianId, now + startsIn, now + startsIn + TimeSpan.FromHours(1), u, now));
        return wo.Id;
    }

    private Task<Result<IReadOnlyList<MyJob>>> List(JobDay day) => Resolve<MyJobsHandler>().Handle(new MyJobsQuery(day), default);

    [Fact]
    public async Task Lists_my_open_jobs_for_today_by_start_and_tomorrows_separately()
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var me = await GivenTechnician();
        var other = await GivenTechnician();
        await Job(me, c, s, "Afternoon", TimeSpan.FromHours(5));
        await Job(me, c, s, "Morning", TimeSpan.FromHours(1));
        await Job(me, c, s, "Tomorrow", TimeSpan.FromHours(20));
        await Job(other, c, s, "Not mine", TimeSpan.FromHours(2));
        var done = await Job(me, c, s, "Done already", TimeSpan.FromHours(3));
        await Advance(done, (w, u, now) => w.Cancel("Not needed", u, now));
        await GivenWorkOrder(c, s, title: "Unassigned");
        SignInTechnician(me);

        var today = (await List(JobDay.Today)).Value;
        today.Select(j => j.Title).ShouldBe(["Morning", "Afternoon"]);
        today[0].CustomerName.ShouldBe("Acme");
        today[0].SiteCity.ShouldBe("Dubai");
        (await List(JobDay.Tomorrow)).Value.Select(j => j.Title).ShouldBe(["Tomorrow"]);
    }

    [Fact]
    public async Task A_job_still_in_progress_from_yesterday_stays_on_today()
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var me = await GivenTechnician();
        var wo = await Job(me, c, s, "Long job", TimeSpan.FromHours(1));
        await Advance(wo, (w, u, now) => w.Dispatch(u, now));
        await Advance(wo, (w, u, now) => w.Start(u, now));
        var waiting = await Job(me, c, s, "Scheduled yesterday", TimeSpan.FromHours(2));
        Fixture.Clock.Advance(TimeSpan.FromDays(1));
        SignInTechnician(me);

        (await List(JobDay.Today)).Value.Select(j => j.Title).ShouldBe(["Long job"]);
        _ = waiting;
    }

    [Fact]
    public async Task Office_users_have_no_job_list()
    {
        await SignInOffice();
        (await List(JobDay.Today)).Error.ShouldBe(Errors.Forbidden);
    }
}
