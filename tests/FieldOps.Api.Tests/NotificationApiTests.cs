using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Notifications;
using FieldOps.Application.Features.Sites;
using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Domain.Identity;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Shouldly;

namespace FieldOps.Api.Tests;

public class NotificationApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;

    private HubConnection Connect(HttpClient client) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Factory.Server.BaseAddress, "hubs/notifications"), o =>
            {
                o.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.AccessTokenProvider = () => Task.FromResult(client.DefaultRequestHeaders.Authorization?.Parameter);
            })
            .Build();

    private static async Task<T> Within<T>(TaskCompletionSource<T> source) =>
        await source.Task.WaitAsync(TimeSpan.FromSeconds(10));

    [Fact]
    public async Task Scheduling_pushes_the_notification_to_the_technician_and_the_change_to_the_office_live()
    {
        var office = await Factory.CreateClientAs(Role.Dispatcher);
        var watcher = await Factory.CreateClientAs(Role.Dispatcher);
        var user = await Factory.CreateUserAsync(Role.Technician);
        var tech = await Factory.LoginAs(user);
        var customer = await Read<CustomerDto>(await office.PostAsJsonAsync("/api/customers",
            new { name = "Acme", phone = "+971500000000", type = "Business" }));
        var site = await Read<SiteDto>(await office.PostAsJsonAsync($"/api/customers/{customer.Id}/sites",
            new { name = "HQ", addressLine1 = "Road", city = "Dubai" }));
        var wo = await Read<WorkOrderDto>(await office.PostAsJsonAsync("/api/work-orders",
            new { customerId = customer.Id, siteId = site.Id, title = "AC not cooling", type = "Repair", priority = "High" }));
        var technicians = await office.GetFromJsonAsync<List<TechnicianAvailabilityDto>>("/api/technicians", Json.Options);
        var technicianId = technicians!.Single(r => r.Technician.UserId == user.Id).Technician.Id;

        var notified = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var techChanges = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var officeChanges = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var officeNotified = new ConcurrentBag<JsonElement>();
        await using var techHub = Connect(tech);
        techHub.On<JsonElement>("NotificationCreated", n => notified.TrySetResult(n));
        techHub.On<JsonElement>("WorkOrderChanged", c => techChanges.TrySetResult(c));
        await techHub.StartAsync();
        await using var officeHub = Connect(watcher);
        officeHub.On<JsonElement>("WorkOrderChanged", c => officeChanges.TrySetResult(c));
        officeHub.On<JsonElement>("NotificationCreated", n => officeNotified.Add(n));
        await officeHub.StartAsync();

        var noon = BusinessCalendar.DayRange(TimeProvider.System.Today()).From.AddHours(12);
        (await office.PostAsJsonAsync($"/api/work-orders/{wo.Id}/schedule", new { technicianId, start = noon, end = noon.AddHours(2) }))
            .EnsureSuccessStatusCode();

        var notification = await Within(notified);
        notification.GetProperty("type").GetString().ShouldBe("JobAssigned");
        notification.GetProperty("link").GetString().ShouldBe($"/tech/jobs/{wo.Id}");
        (await Within(techChanges)).GetProperty("status").GetString().ShouldBe("Scheduled");
        var change = await Within(officeChanges);
        change.GetProperty("id").GetGuid().ShouldBe(wo.Id);
        officeNotified.ShouldBeEmpty();

        var list = await tech.GetFromJsonAsync<NotificationList>("/api/notifications", Json.Options);
        list!.UnreadCount.ShouldBe(1);
        var id = list.Items.ShouldHaveSingleItem().Id;
        (await office.PostAsync($"/api/notifications/{id}/read", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await tech.PostAsync($"/api/notifications/{id}/read", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await tech.PostAsync("/api/notifications/read-all", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await tech.GetFromJsonAsync<NotificationList>("/api/notifications?unreadOnly=true", Json.Options))!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Notifications_and_the_hub_need_a_signed_in_user()
    {
        var anonymous = Factory.CreateClient();
        (await anonymous.GetAsync("/api/notifications")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/notifications/read-all", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await using var hub = Connect(anonymous);
        await Should.ThrowAsync<HttpRequestException>(() => hub.StartAsync());

        var admin = await Factory.CreateClientAs(Role.Admin);
        (await admin.GetFromJsonAsync<NotificationList>("/api/notifications", Json.Options))!.Items.ShouldBeEmpty();
    }
}
