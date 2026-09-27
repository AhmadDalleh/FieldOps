using FieldOps.Application.Features.Dashboard;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Technicians;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Application.Tests.Dashboard;

public class DashboardTests(PostgresFixture fixture) : TestBase(fixture)
{
    private Task<DashboardDto> Dashboard() => Resolve<GetDashboardHandler>().Handle(new GetDashboardQuery(), default);

    [Fact]
    public async Task Dashboard_counts_open_jobs_overdue_and_completed_today_and_who_is_busy_free_or_off()
    {
        await SignInOffice();
        var (customer, site) = await GivenCustomerAndSite("Al Noor Trading");
        var now = Fixture.Clock.GetUtcNow();
        var working = await GivenTechnician();
        var waiting = await GivenTechnician();
        var away = await GivenTechnician();

        var lateUrgent = await GivenWorkOrder(customer, site, priority: WorkOrderPriority.Urgent, title: "Chiller down", dueBy: now.AddHours(1));
        var soonUrgent = await GivenWorkOrder(customer, site, priority: WorkOrderPriority.Urgent, title: "Leak", dueBy: now.AddHours(3));
        await GivenWorkOrder(customer, site, priority: WorkOrderPriority.Low);
        var scheduled = await GivenWorkOrder(customer, site);
        await Assign(scheduled.Id, waiting.TechnicianId);
        await Complete((await GivenWorkOrder(customer, site)).Id, working.TechnicianId);
        var onSite = await GivenWorkOrder(customer, site);
        await Assign(onSite.Id, working.TechnicianId);
        await Advance(onSite.Id, (w, u, t) => w.Dispatch(u, t));
        await Advance(onSite.Id, (w, u, t) => w.Start(u, t));
        var timeOff = TimeOff.Request(away.TechnicianId, now.AddHours(-1), now.AddHours(8), "Doctor").Value;
        timeOff.Approve();
        var db = NewDb();
        db.TimeOffs.Add(timeOff);
        await db.SaveChangesAsync();
        Fixture.Clock.Advance(TimeSpan.FromHours(2)); // the first urgent job is now overdue

        var dashboard = await Dashboard();

        dashboard.Date.ShouldBe(new DateOnly(2026, 10, 1));
        dashboard.OpenByStatus.Select(s => (s.Status, s.Count)).ShouldBe([
            (WorkOrderStatus.New, 3), (WorkOrderStatus.Scheduled, 1), (WorkOrderStatus.Dispatched, 0),
            (WorkOrderStatus.EnRoute, 0), (WorkOrderStatus.InProgress, 1), (WorkOrderStatus.OnHold, 0),
        ]);
        (dashboard.Unassigned, dashboard.Overdue, dashboard.CompletedToday).ShouldBe((3, 1, 1));
        (dashboard.TechniciansBusy, dashboard.TechniciansFree, dashboard.TechniciansOff).ShouldBe((1, 1, 1));
        var busy = dashboard.Technicians[0];
        (busy.Id, busy.State, busy.CurrentJobNumber, busy.JobsToday).ShouldBe((working.TechnicianId, TechnicianState.Busy, onSite.Number, 2));
        dashboard.Technicians.Single(t => t.Id == away.TechnicianId).State.ShouldBe(TechnicianState.Off);
        dashboard.UrgentUnassigned.Select(j => (j.Id, j.IsOverdue, j.CustomerName)).ShouldBe([
            (lateUrgent.Id, true, "Al Noor Trading"), (soonUrgent.Id, false, "Al Noor Trading"),
        ]);
    }

    [Fact]
    public async Task An_empty_company_shows_zeros()
    {
        var dashboard = await Dashboard();

        dashboard.OpenByStatus.ShouldAllBe(s => s.Count == 0);
        (dashboard.Unassigned, dashboard.Overdue, dashboard.CompletedToday, dashboard.Technicians.Count).ShouldBe((0, 0, 0, 0));
        dashboard.UrgentUnassigned.ShouldBeEmpty();
    }
}
