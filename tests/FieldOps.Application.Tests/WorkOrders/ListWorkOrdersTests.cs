using FieldOps.Application.Common;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Application.Tests.WorkOrders;

public class ListWorkOrdersTests(PostgresFixture fixture) : TestBase(fixture)
{
    private static readonly DateTimeOffset Now = PostgresFixture.Start;

    private async Task<IReadOnlyList<WorkOrderListItem>> List(
        IReadOnlyList<WorkOrderStatus>? statuses = null, WorkOrderPriority? priority = null, WorkOrderType? type = null,
        Guid? technicianId = null, Guid? customerId = null, DateOnly? from = null, DateOnly? to = null,
        string? search = null, string? sort = null) =>
        (await Resolve<ListWorkOrdersHandler>().Handle(new ListWorkOrdersQuery(
            new PageRequest(1, 50, search, sort), statuses, priority, type, technicianId, customerId, null, from, to), default)).Items;

    [Fact]
    public async Task Default_sort_is_urgent_first_then_due_soonest_with_undated_last()
    {
        var (c, s) = await GivenCustomerAndSite();
        await GivenWorkOrder(c, s, priority: WorkOrderPriority.Low, title: "low");
        await GivenWorkOrder(c, s, priority: WorkOrderPriority.High, title: "high-undated");
        await GivenWorkOrder(c, s, priority: WorkOrderPriority.High, title: "high-late", dueBy: Now.AddDays(3));
        await GivenWorkOrder(c, s, priority: WorkOrderPriority.High, title: "high-soon", dueBy: Now.AddDays(1));
        await GivenWorkOrder(c, s, priority: WorkOrderPriority.Urgent, title: "urgent");
        await GivenWorkOrder(c, s, priority: WorkOrderPriority.Medium, title: "medium");

        (await List()).Select(w => w.Title).ShouldBe(["urgent", "high-soon", "high-late", "high-undated", "medium", "low"]);
    }

    [Fact]
    public async Task Overdue_means_past_due_and_still_open()
    {
        var (c, s) = await GivenCustomerAndSite();
        var (_, tech) = await GivenTechnician();
        await GivenWorkOrder(c, s, title: "late", dueBy: Now.AddHours(-1));
        var done = await GivenWorkOrder(c, s, title: "late-but-done", dueBy: Now.AddHours(-1));
        await Complete(done.Id, tech);
        await GivenWorkOrder(c, s, title: "future", dueBy: Now.AddHours(1));
        await GivenWorkOrder(c, s, title: "undated");

        var overdue = (await List()).ToDictionary(w => w.Title, w => w.IsOverdue);

        overdue.ShouldBe(new Dictionary<string, bool> { ["late"] = true, ["late-but-done"] = false, ["future"] = false, ["undated"] = false },
            ignoreOrder: true);
    }

    [Fact]
    public async Task Filters_by_several_statuses_priority_type_customer_and_technician()
    {
        var (c1, s1) = await GivenCustomerAndSite("Acme");
        var (c2, s2) = await GivenCustomerAndSite("Other");
        var (_, tech) = await GivenTechnician();
        var assigned = await GivenWorkOrder(c1, s1, title: "assigned", type: WorkOrderType.Maintenance);
        await Assign(assigned.Id, tech);
        var cancelled = await GivenWorkOrder(c1, s1, title: "cancelled");
        await Resolve<CancelWorkOrderHandler>().Handle(new CancelWorkOrderCommand(cancelled.Id, new CancelInput("Dup")), default);
        await GivenWorkOrder(c2, s2, title: "other-urgent", priority: WorkOrderPriority.Urgent);

        (await List(statuses: [WorkOrderStatus.Scheduled, WorkOrderStatus.Cancelled])).Select(w => w.Title)
            .ShouldBe(["assigned", "cancelled"], ignoreOrder: true);
        (await List(priority: WorkOrderPriority.Urgent)).ShouldHaveSingleItem().Title.ShouldBe("other-urgent");
        (await List(type: WorkOrderType.Maintenance)).ShouldHaveSingleItem().Title.ShouldBe("assigned");
        (await List(customerId: c2)).ShouldHaveSingleItem().CustomerName.ShouldBe("Other");
        var mine = (await List(technicianId: tech)).ShouldHaveSingleItem();
        mine.TechnicianName.ShouldBe("Technician User");
        mine.SiteName.ShouldBe("HQ");
    }

    [Fact]
    public async Task Filters_by_Dubai_creation_date_range()
    {
        var (c, s) = await GivenCustomerAndSite();
        await GivenWorkOrder(c, s, title: "1 Oct");                   // 06:00 UTC = 10:00 Dubai, 1 Oct
        Fixture.Clock.Advance(TimeSpan.FromHours(15));                // 21:00 UTC = 01:00 Dubai, 2 Oct
        await GivenWorkOrder(c, s, title: "2 Oct");

        (await List(from: new DateOnly(2026, 10, 2))).ShouldHaveSingleItem().Title.ShouldBe("2 Oct");
        (await List(to: new DateOnly(2026, 10, 1))).ShouldHaveSingleItem().Title.ShouldBe("1 Oct");
        (await List(from: new DateOnly(2026, 10, 1), to: new DateOnly(2026, 10, 2))).Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("000002", "Leaking pipe")]
    [InlineData("wo-000002", "Leaking pipe")]
    [InlineData("LEAK", "Leaking pipe")]
    public async Task Searches_number_and_title(string search, string expected)
    {
        var (c, s) = await GivenCustomerAndSite();
        await GivenWorkOrder(c, s, title: "AC not cooling");
        await GivenWorkOrder(c, s, title: "Leaking pipe");

        (await List(search: search)).ShouldHaveSingleItem().Title.ShouldBe(expected);
    }

    [Fact]
    public async Task Search_treats_wildcards_literally()
    {
        var (c, s) = await GivenCustomerAndSite();
        await GivenWorkOrder(c, s, title: "AC not cooling");

        (await List(search: "%")).ShouldBeEmpty();
    }
}
