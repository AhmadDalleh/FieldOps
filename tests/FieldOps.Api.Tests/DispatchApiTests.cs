using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Dispatch;
using FieldOps.Application.Features.Sites;
using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Domain.Identity;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Api.Tests;

public class DispatchApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly DateTimeOffset Morning = new(2026, 12, 1, 4, 0, 0, TimeSpan.Zero);

    private static async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;

    private async Task<(HttpClient Office, Guid CustomerId, Guid SiteId, Guid Tech, HttpClient TechClient)> Given()
    {
        var office = await Factory.CreateClientAs(Role.Dispatcher);
        var customer = await Read<CustomerDto>(await office.PostAsJsonAsync("/api/customers",
            new { name = "Acme", phone = "+971500000000", type = "Business" }));
        var site = await Read<SiteDto>(await office.PostAsJsonAsync($"/api/customers/{customer.Id}/sites",
            new { name = "HQ", addressLine1 = "Road", city = "Dubai", latitude = 25.2, longitude = 55.3 }));
        var user = await Factory.CreateUserAsync(Role.Technician);
        var list = await office.GetFromJsonAsync<List<TechnicianAvailabilityDto>>("/api/technicians", Json.Options);
        return (office, customer.Id, site.Id, list!.Single(r => r.Technician.UserId == user.Id).Technician.Id, await Factory.LoginAs(user));
    }

    private static async Task<WorkOrderDto> Create(HttpClient office, Guid customerId, Guid siteId) =>
        await Read<WorkOrderDto>(await office.PostAsJsonAsync("/api/work-orders",
            new { customerId, siteId, title = "AC not cooling", type = "Repair", priority = "High" }));

    private static object Slot(Guid technicianId, int fromHour, int hours, bool allowOverlap = false) =>
        new { technicianId, start = Morning.AddHours(fromHour), end = Morning.AddHours(fromHour + hours), allowOverlap };

    [Fact]
    public async Task Office_schedules_dispatches_and_unassigns()
    {
        var (office, c, s, tech, _) = await Given();
        var wo = await Create(office, c, s);

        var scheduled = await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/schedule", Slot(tech, 0, 2));
        var dispatched = await office.PostAsync($"/api/work-orders/{wo.Id}/dispatch", null);
        var unassigned = await office.PostAsync($"/api/work-orders/{wo.Id}/unassign", null);

        scheduled.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Read<ScheduleResult>(scheduled)).WorkOrder.Status.ShouldBe(WorkOrderStatus.Scheduled);
        (await Read<WorkOrderDto>(dispatched)).Status.ShouldBe(WorkOrderStatus.Dispatched);
        (await Read<WorkOrderDto>(unassigned)).Status.ShouldBe(WorkOrderStatus.New);
    }

    [Fact]
    public async Task Overlap_returns_409_with_the_conflicting_jobs_and_saves_with_allow_overlap()
    {
        var (office, c, s, tech, _) = await Given();
        var first = await Create(office, c, s);
        var second = await Create(office, c, s);
        await office.PostAsJsonAsync($"/api/work-orders/{first.Id}/schedule", Slot(tech, 0, 2));

        var blocked = await office.PostAsJsonAsync($"/api/work-orders/{second.Id}/schedule", Slot(tech, 1, 2));
        var forced = await office.PostAsJsonAsync($"/api/work-orders/{second.Id}/schedule", Slot(tech, 1, 2, allowOverlap: true));

        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("code").GetString().ShouldBe("Schedule.Overlap");
        var conflict = problem.RootElement.GetProperty("conflicts").EnumerateArray().ShouldHaveSingleItem();
        conflict.GetProperty("number").GetString().ShouldBe(first.Number);
        forced.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Read<ScheduleResult>(forced)).Warnings.ShouldBe([$"Overlaps {first.Number}"]);
    }

    [Fact]
    public async Task Bad_duration_returns_400()
    {
        var (office, c, s, tech, _) = await Given();
        var wo = await Create(office, c, s);

        var response = await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/schedule",
            new { technicianId = tech, start = Morning, end = Morning.AddMinutes(10) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dispatch_day_and_board()
    {
        var (office, c, s, tech, _) = await Given();
        var wo = await Create(office, c, s);
        await Create(office, c, s);
        await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/schedule", Slot(tech, 0, 2));

        var day = await office.PostAsJsonAsync("/api/work-orders/dispatch-day", new { technicianId = tech, date = "2026-12-01" });
        var board = await office.GetFromJsonAsync<DispatchBoard>("/api/dispatch/board?date=2026-12-01", Json.Options);

        (await Read<DispatchDayResult>(day)).Dispatched.ShouldBe([wo.Number]);
        board!.Jobs.ShouldHaveSingleItem().Status.ShouldBe(WorkOrderStatus.Dispatched);
        board.Unassigned.Count.ShouldBe(1);
        board.Technicians.ShouldContain(t => t.Id == tech);
    }

    [Fact]
    public async Task Technician_cannot_use_scheduling_or_the_board()
    {
        var (office, c, s, tech, techClient) = await Given();
        var wo = await Create(office, c, s);
        await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/schedule", Slot(tech, 0, 2));

        var responses = new[]
        {
            await techClient.PostAsJsonAsync($"/api/work-orders/{wo.Id}/schedule", Slot(tech, 3, 1)),
            await techClient.PostAsync($"/api/work-orders/{wo.Id}/dispatch", null),
            await techClient.PostAsync($"/api/work-orders/{wo.Id}/unassign", null),
            await techClient.PostAsJsonAsync("/api/work-orders/dispatch-day", new { technicianId = tech, date = "2026-12-01" }),
            await techClient.GetAsync("/api/dispatch/board"),
        };

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Forbidden);
    }
}
