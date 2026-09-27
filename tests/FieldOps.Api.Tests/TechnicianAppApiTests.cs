using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Me;
using FieldOps.Application.Features.Sites;
using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Domain.Identity;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Api.Tests;

public class TechnicianAppApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private static async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;

    /// <summary>A job scheduled for noon today (Dubai) and dispatched to a new technician.</summary>
    private async Task<(HttpClient Office, HttpClient Tech, WorkOrderDto WorkOrder)> GivenDispatchedJob()
    {
        var office = await Factory.CreateClientAs(Role.Dispatcher);
        var customer = await Read<CustomerDto>(await office.PostAsJsonAsync("/api/customers",
            new { name = "Acme", phone = "+971500000000", type = "Business" }));
        var site = await Read<SiteDto>(await office.PostAsJsonAsync($"/api/customers/{customer.Id}/sites",
            new { name = "HQ", addressLine1 = "Road", city = "Dubai", latitude = 25.2, longitude = 55.27 }));
        var wo = await Read<WorkOrderDto>(await office.PostAsJsonAsync("/api/work-orders",
            new { customerId = customer.Id, siteId = site.Id, title = "AC not cooling", type = "Repair", priority = "High" }));

        var user = await Factory.CreateUserAsync(Role.Technician);
        var technicians = await office.GetFromJsonAsync<List<TechnicianAvailabilityDto>>("/api/technicians", Json.Options);
        var technicianId = technicians!.Single(r => r.Technician.UserId == user.Id).Technician.Id;
        var noon = BusinessCalendar.DayRange(TimeProvider.System.Today()).From.AddHours(12);
        (await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/schedule",
            new { technicianId, start = noon, end = noon.AddHours(2) })).EnsureSuccessStatusCode();
        (await office.PostAsync($"/api/work-orders/{wo.Id}/dispatch", null)).EnsureSuccessStatusCode();
        return (office, await Factory.LoginAs(user), wo);
    }

    private static MultipartFormDataContent File(string kind, string contentType = "image/png", string name = "photo.png")
    {
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", name }, { new StringContent(kind), "kind" } };
    }

    [Fact]
    public async Task Technician_goes_en_route_starts_takes_a_photo_and_completes_with_a_signature()
    {
        var (office, tech, wo) = await GivenDispatchedJob();

        var today = await tech.GetFromJsonAsync<List<MyJob>>("/api/me/jobs?day=Today", Json.Options);
        today!.ShouldHaveSingleItem().Number.ShouldBe(wo.Number);
        (await tech.GetFromJsonAsync<List<MyJob>>("/api/me/jobs?day=Tomorrow", Json.Options))!.ShouldBeEmpty();

        var enRoute = await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/en-route", new { lat = 25.2, lng = 55.27 });
        (await Read<WorkOrderDto>(enRoute)).Status.ShouldBe(WorkOrderStatus.EnRoute);
        var started = await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/start", new { });
        (await Read<WorkOrderDto>(started)).Status.ShouldBe(WorkOrderStatus.InProgress);

        var photo = await tech.PostAsync($"/api/work-orders/{wo.Id}/attachments", File("Photo"));
        photo.StatusCode.ShouldBe(HttpStatusCode.Created);
        var photoDto = await Read<AttachmentDto>(photo);
        var download = await office.GetAsync($"/api/attachments/{photoDto.Id}");
        download.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(Png);

        var signature = await Read<AttachmentDto>(await tech.PostAsync($"/api/work-orders/{wo.Id}/attachments",
            File("Signature", name: "signature.png")));
        var completed = await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/complete",
            new { completionNotes = "Replaced the capacitor", signedByName = "Sara M.", signatureAttachmentId = signature.Id });
        completed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Read<WorkOrderDto>(completed)).Status.ShouldBe(WorkOrderStatus.Completed);

        var entries = await tech.GetFromJsonAsync<List<TimeEntryDto>>($"/api/work-orders/{wo.Id}/time-entries", Json.Options);
        entries.ShouldNotBeNull();
        entries.Select(e => e.Type).ShouldBe([TimeEntryType.Travel, TimeEntryType.Work]);
        entries.ShouldAllBe(e => e.EndedAt != null && !e.CanEdit);
        var late = await tech.PutAsJsonAsync($"/api/time-entries/{entries[1].Id}",
            new { startedAt = entries[1].StartedAt, endedAt = entries[1].EndedAt });
        late.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await tech.DeleteAsync($"/api/attachments/{photoDto.Id}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Field_actions_belong_to_the_assigned_technician()
    {
        var (office, _, wo) = await GivenDispatchedJob();
        var stranger = await Factory.CreateClientAs(Role.Technician);

        (await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/en-route", new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await stranger.PostAsJsonAsync($"/api/work-orders/{wo.Id}/en-route", new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await stranger.PostAsJsonAsync($"/api/work-orders/{wo.Id}/start", new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await stranger.GetAsync($"/api/work-orders/{wo.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await stranger.GetAsync($"/api/work-orders/{wo.Id}/attachments")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await stranger.GetAsync($"/api/work-orders/{wo.Id}/time-entries")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await office.GetAsync("/api/me/jobs")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Factory.CreateClient().GetAsync("/api/me/jobs")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Bad_input_is_rejected()
    {
        var (_, tech, wo) = await GivenDispatchedJob();

        (await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/en-route", new { lat = 200, lng = 55 }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await tech.PostAsync($"/api/work-orders/{wo.Id}/attachments", File("Photo")))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/start", new { });
        (await tech.PostAsync($"/api/work-orders/{wo.Id}/attachments", File("Photo", "image/gif", "a.gif")))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await tech.PostAsync($"/api/work-orders/{wo.Id}/attachments", File("Document", "application/pdf", "a.pdf")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/complete", new { signedByName = "Sara" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
