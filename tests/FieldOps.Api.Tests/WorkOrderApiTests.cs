using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Sites;
using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Domain.Identity;
using FieldOps.Domain.WorkOrders;
using FieldOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace FieldOps.Api.Tests;

public class WorkOrderApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;

    private async Task<(HttpClient Office, Guid CustomerId, Guid SiteId)> GivenSite()
    {
        var office = await Factory.CreateClientAs(Role.Dispatcher);
        var customer = await Read<CustomerDto>(await office.PostAsJsonAsync("/api/customers",
            new { name = "Acme", phone = "+971500000000", type = "Business" }));
        var site = await Read<SiteDto>(await office.PostAsJsonAsync($"/api/customers/{customer.Id}/sites",
            new { name = "HQ", addressLine1 = "Road", city = "Dubai" }));
        return (office, customer.Id, site.Id);
    }

    private static object NewWorkOrder(Guid customerId, Guid siteId, string priority = "High") =>
        new { customerId, siteId, title = "AC not cooling", type = "Repair", priority };

    private async Task<WorkOrderDto> Create(HttpClient office, Guid customerId, Guid siteId) =>
        await Read<WorkOrderDto>(await office.PostAsJsonAsync("/api/work-orders", NewWorkOrder(customerId, siteId)));

    private async Task<(HttpClient Client, Guid TechnicianId)> GivenTechnician(HttpClient office)
    {
        var user = await Factory.CreateUserAsync(Role.Technician);
        var list = await office.GetFromJsonAsync<List<TechnicianAvailabilityDto>>("/api/technicians", Json.Options);
        return (await Factory.LoginAs(user), list!.Single(r => r.Technician.UserId == user.Id).Technician.Id);
    }

    /// <summary>Scheduling arrives with Phase 5; until then tests assign and start jobs through the domain.</summary>
    private async Task Advance(Guid id, Func<WorkOrder, Guid, DateTimeOffset, Domain.Common.Result> step)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var wo = await db.WorkOrders.SingleAsync(w => w.Id == id);
        var userId = await db.Users.Select(u => u.Id).FirstAsync();
        step(wo, userId, DateTimeOffset.UtcNow).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync();
    }

    private async Task StartFor(Guid id, Guid technicianId)
    {
        await Advance(id, (w, u, now) => w.Schedule(technicianId, now.AddHours(1), now.AddHours(2), u, now));
        await Advance(id, (w, u, now) => w.Dispatch(u, now));
        await Advance(id, (w, u, now) => w.Start(u, now));
    }

    [Fact]
    public async Task Office_creates_lists_reads_and_edits_a_work_order()
    {
        var (office, customerId, siteId) = await GivenSite();

        var created = await office.PostAsJsonAsync("/api/work-orders", NewWorkOrder(customerId, siteId));
        var wo = await Read<WorkOrderDto>(created);
        var list = await office.GetFromJsonAsync<PagedResult<WorkOrderListItem>>("/api/work-orders?status=New,Scheduled&priority=High", Json.Options);
        var updated = await office.PutAsJsonAsync($"/api/work-orders/{wo.Id}",
            new { title = "Replace compressor", type = "Repair", priority = "Urgent", version = wo.Version });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.Location!.ToString().ShouldBe($"/api/work-orders/{wo.Id}");
        wo.Number.ShouldBe("WO-000001");
        list!.Items.ShouldHaveSingleItem().Number.ShouldBe("WO-000001");
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Read<WorkOrderDto>(updated)).Title.ShouldBe("Replace compressor");
    }

    [Fact]
    public async Task Json_contract_uses_enum_names()
    {
        var (office, customerId, siteId) = await GivenSite();
        var created = await office.PostAsJsonAsync("/api/work-orders", NewWorkOrder(customerId, siteId, "Urgent"));

        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("status").GetString().ShouldBe("New");
        json.RootElement.GetProperty("priority").GetString().ShouldBe("Urgent");
        json.RootElement.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).ShouldBe(["Schedule", "Cancel"]);
        json.RootElement.GetProperty("dueBy").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task Stale_version_returns_409_with_a_reload_message()
    {
        var (office, customerId, siteId) = await GivenSite();
        var wo = await Create(office, customerId, siteId);
        var body = new { title = "First", type = "Repair", priority = "Low", version = wo.Version };
        await office.PutAsJsonAsync($"/api/work-orders/{wo.Id}", body);

        var second = await office.PutAsJsonAsync($"/api/work-orders/{wo.Id}", body with { title = "Second" });

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadAsStringAsync();
        problem.ShouldContain("WorkOrder.ConcurrencyConflict");
        problem.ShouldContain("Reload");
    }

    [Fact]
    public async Task Invalid_input_returns_400_and_unknown_status_filter_returns_400()
    {
        var (office, customerId, _) = await GivenSite();

        (await office.PostAsJsonAsync("/api/work-orders", new { customerId, title = "", type = "Repair", priority = "Low" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await office.GetAsync("/api/work-orders?status=Nope")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invalid_transition_returns_409()
    {
        var (office, customerId, siteId) = await GivenSite();
        var wo = await Create(office, customerId, siteId);

        var response = await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/resume", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("WorkOrder.InvalidTransition");
    }

    [Fact]
    public async Task Office_manages_tasks_notes_hold_resume_cancel_and_reads_the_timeline()
    {
        var (office, customerId, siteId) = await GivenSite();
        var (_, tech) = await GivenTechnician(office);
        var wo = await Create(office, customerId, siteId);

        wo = await Read<WorkOrderDto>(await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/tasks", new { description = "One" }));
        wo = await Read<WorkOrderDto>(await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/tasks", new { description = "Two" }));
        var ids = wo.Tasks.Select(t => t.Id).ToArray();
        (await office.PutAsJsonAsync($"/api/work-orders/{wo.Id}/tasks/{ids[0]}", new { description = "Uno" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/tasks/reorder", new { taskIds = new[] { ids[1], ids[0] } })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await office.PostAsync($"/api/work-orders/{wo.Id}/tasks/{ids[1]}/toggle", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await office.DeleteAsync($"/api/work-orders/{wo.Id}/tasks/{ids[1]}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var note = await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/notes", new { body = "Call first", isInternal = true });
        note.StatusCode.ShouldBe(HttpStatusCode.Created);
        var noteId = (await Read<NoteDto>(note)).Id;
        (await office.PutAsJsonAsync($"/api/work-orders/{wo.Id}/notes/{noteId}", new { body = "Call first!", isInternal = true })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await office.GetFromJsonAsync<List<NoteDto>>($"/api/work-orders/{wo.Id}/notes", Json.Options))!.Single().Body.ShouldBe("Call first!");

        await StartFor(wo.Id, tech);
        (await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/hold", new { note = "Parts" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/resume", new { })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var cancelled = await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/cancel", new { reason = "Customer request" });
        (await Read<WorkOrderDto>(cancelled)).Status.ShouldBe(WorkOrderStatus.Cancelled);

        var history = await office.GetFromJsonAsync<List<WorkOrderHistoryItem>>($"/api/work-orders/{wo.Id}/history", Json.Options);
        history!.Select(h => h.ToStatus).ShouldBe([WorkOrderStatus.New, WorkOrderStatus.Scheduled, WorkOrderStatus.Dispatched,
            WorkOrderStatus.InProgress, WorkOrderStatus.OnHold, WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled]);
    }

    [Fact]
    public async Task Assigned_technician_reaches_only_the_technician_routes_of_their_own_job()
    {
        var (office, customerId, siteId) = await GivenSite();
        var (tech, techId) = await GivenTechnician(office);
        var wo = await Create(office, customerId, siteId);
        wo = await Read<WorkOrderDto>(await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/tasks", new { description = "One" }));
        await StartFor(wo.Id, techId);
        var taskId = wo.Tasks[0].Id;

        var allowed = new[]
        {
            await tech.GetAsync($"/api/work-orders/{wo.Id}"),
            await tech.GetAsync($"/api/work-orders/{wo.Id}/history"),
            await tech.GetAsync($"/api/work-orders/{wo.Id}/notes"),
            await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/notes", new { body = "On site", isInternal = false }),
            await tech.PostAsync($"/api/work-orders/{wo.Id}/tasks/{taskId}/toggle", null),
            await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/hold", new { note = "Need ladder" }),
            await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/resume", new { }),
        };
        var officeOnly = new[]
        {
            await tech.GetAsync("/api/work-orders"),
            await tech.PostAsJsonAsync("/api/work-orders", NewWorkOrder(customerId, siteId)),
            await tech.PutAsJsonAsync($"/api/work-orders/{wo.Id}", new { title = "X", type = "Repair", priority = "Low", version = 0 }),
            await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/tasks", new { description = "More" }),
            await tech.PutAsJsonAsync($"/api/work-orders/{wo.Id}/tasks/{taskId}", new { description = "Changed" }),
            await tech.DeleteAsync($"/api/work-orders/{wo.Id}/tasks/{taskId}"),
            await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/tasks/reorder", new { taskIds = new[] { taskId } }),
            await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/cancel", new { reason = "No" }),
        };

        allowed.Select(r => r.StatusCode).ShouldAllBe(s => s == HttpStatusCode.OK || s == HttpStatusCode.Created);
        officeOnly.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Technician_gets_403_on_another_technicians_job()
    {
        var (office, customerId, siteId) = await GivenSite();
        var (_, assigned) = await GivenTechnician(office);
        var (other, _) = await GivenTechnician(office);
        var wo = await Create(office, customerId, siteId);
        wo = await Read<WorkOrderDto>(await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/tasks", new { description = "One" }));
        await StartFor(wo.Id, assigned);

        var responses = new[]
        {
            await other.GetAsync($"/api/work-orders/{wo.Id}"),
            await other.GetAsync($"/api/work-orders/{wo.Id}/history"),
            await other.GetAsync($"/api/work-orders/{wo.Id}/notes"),
            await other.PostAsJsonAsync($"/api/work-orders/{wo.Id}/notes", new { body = "Hi", isInternal = false }),
            await other.PostAsync($"/api/work-orders/{wo.Id}/tasks/{wo.Tasks[0].Id}/toggle", null),
            await other.PostAsJsonAsync($"/api/work-orders/{wo.Id}/hold", new { note = "x" }),
        };

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        (await Factory.CreateClient().GetAsync("/api/work-orders")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Customer_and_site_with_open_work_orders_cannot_be_deactivated()
    {
        var (office, customerId, siteId) = await GivenSite();
        await Create(office, customerId, siteId);

        (await office.PostAsync($"/api/customers/{customerId}/deactivate", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await office.PostAsync($"/api/sites/{siteId}/deactivate", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
