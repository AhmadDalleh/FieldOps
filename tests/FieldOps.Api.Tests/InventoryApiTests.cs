using System.Net;
using System.Net.Http.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Inventory;
using FieldOps.Application.Features.Me;
using FieldOps.Application.Features.Sites;
using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Inventory;
using Shouldly;

namespace FieldOps.Api.Tests;

public class InventoryApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly Guid Warehouse = StockLocation.MainWarehouseId;

    private static async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;

    private static object NewPart(string sku = "FLT-01") =>
        new { sku, name = "Air filter", unit = "Pcs", unitCost = 8, unitPrice = 15, reorderLevel = 5 };

    [Fact]
    public async Task Admin_manages_the_catalog_and_stock_while_dispatchers_read_receive_and_transfer()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        var dispatcher = await Factory.CreateClientAs(Role.Dispatcher);
        var technician = await Factory.CreateClientAs(Role.Technician);

        (await dispatcher.PostAsJsonAsync("/api/parts", NewPart())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var created = await admin.PostAsJsonAsync("/api/parts", NewPart());
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var part = await Read<PartDto>(created);
        (await admin.PostAsJsonAsync("/api/parts", NewPart("flt-01"))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync("/api/parts", new { sku = "X", name = "X", unit = "Pcs", unitCost = -1, unitPrice = 1, reorderLevel = 0 }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var parts = await dispatcher.GetFromJsonAsync<PagedResult<PartDto>>("/api/parts?search=filter", Json.Options);
        parts!.Items.ShouldHaveSingleItem().Id.ShouldBe(part.Id);
        (await technician.GetAsync("/api/parts")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var locations = await dispatcher.GetFromJsonAsync<List<StockLocationDto>>("/api/stock-locations", Json.Options);
        locations![0].Id.ShouldBe(Warehouse); // warehouses first, then vans by name
        var van = locations.First(l => l.Type == StockLocationType.Van);
        (await dispatcher.PostAsJsonAsync("/api/stock/receive", new { partId = part.Id, locationId = Warehouse, quantity = 10 }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await dispatcher.PostAsJsonAsync("/api/stock/transfer",
            new { partId = part.Id, fromLocationId = Warehouse, toLocationId = van.Id, quantity = 20 })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var moved = await Read<StockRow>(await dispatcher.PostAsJsonAsync("/api/stock/transfer",
            new { partId = part.Id, fromLocationId = Warehouse, toLocationId = van.Id, quantity = 4 }));
        moved.Total.ShouldBe(10);

        (await dispatcher.PostAsJsonAsync("/api/stock/adjust", new { partId = part.Id, locationId = Warehouse, newQuantity = 5, reason = "Count" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync("/api/stock/adjust", new { partId = part.Id, locationId = Warehouse, newQuantity = 5 }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var adjusted = await Read<StockRow>(await admin.PostAsJsonAsync("/api/stock/adjust",
            new { partId = part.Id, locationId = Warehouse, newQuantity = 5, reason = "Count" }));
        adjusted.Total.ShouldBe(9);

        var low = await dispatcher.GetFromJsonAsync<List<StockRow>>("/api/stock?lowOnly=true", Json.Options);
        low!.ShouldBeEmpty();
        var history = await dispatcher.GetFromJsonAsync<PagedResult<StockMovementDto>>(
            $"/api/stock/movements?partId={part.Id}&type=Transfer", Json.Options);
        history!.Items.ShouldHaveSingleItem().Quantity.ShouldBe(4);
        (await technician.GetAsync("/api/stock")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Technician_uses_van_parts_on_their_job_and_returns_them()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        var part = await Read<PartDto>(await admin.PostAsJsonAsync("/api/parts", NewPart()));
        var customer = await Read<CustomerDto>(await admin.PostAsJsonAsync("/api/customers",
            new { name = "Acme", phone = "+971500000000", type = "Business" }));
        var site = await Read<SiteDto>(await admin.PostAsJsonAsync($"/api/customers/{customer.Id}/sites",
            new { name = "HQ", addressLine1 = "Road", city = "Dubai" }));
        var wo = await Read<WorkOrderDto>(await admin.PostAsJsonAsync("/api/work-orders",
            new { customerId = customer.Id, siteId = site.Id, title = "AC", type = "Repair", priority = "High" }));

        var user = await Factory.CreateUserAsync(Role.Technician);
        var technicians = await admin.GetFromJsonAsync<List<TechnicianAvailabilityDto>>("/api/technicians", Json.Options);
        var technicianId = technicians!.Single(r => r.Technician.UserId == user.Id).Technician.Id;
        var locations = await admin.GetFromJsonAsync<List<StockLocationDto>>("/api/stock-locations", Json.Options);
        var van = locations!.Single(l => l.TechnicianId == technicianId);
        await admin.PostAsJsonAsync("/api/stock/receive", new { partId = part.Id, locationId = Warehouse, quantity = 5 });
        await admin.PostAsJsonAsync("/api/stock/transfer", new { partId = part.Id, fromLocationId = Warehouse, toLocationId = van.Id, quantity = 3 });
        var start = DateTimeOffset.UtcNow;
        await admin.PostAsJsonAsync($"/api/work-orders/{wo.Id}/schedule", new { technicianId, start, end = start.AddHours(2) });
        await admin.PostAsync($"/api/work-orders/{wo.Id}/dispatch", null);
        var tech = await Factory.LoginAs(user);
        await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/start", new { });

        var vanStock = await tech.GetFromJsonAsync<List<VanStockItem>>("/api/me/van-stock", Json.Options);
        vanStock!.ShouldHaveSingleItem().Quantity.ShouldBe(3);
        (await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/parts", new { partId = part.Id, quantity = 4 }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var lines = await Read<List<WorkOrderPartDto>>(await tech.PostAsJsonAsync($"/api/work-orders/{wo.Id}/parts",
            new { partId = part.Id, quantity = 2 }));
        lines.ShouldHaveSingleItem().LineTotal.ShouldBe(30m);
        (await admin.PostAsJsonAsync($"/api/work-orders/{wo.Id}/cancel", new { reason = "No longer needed" }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var stranger = await Factory.CreateClientAs(Role.Technician);
        (await stranger.GetAsync($"/api/work-orders/{wo.Id}/parts")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.GetAsync("/api/me/van-stock")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var removed = await tech.DeleteAsync($"/api/work-orders/{wo.Id}/parts/{lines[0].Id}");
        (await Read<List<WorkOrderPartDto>>(removed)).ShouldBeEmpty();
        (await tech.GetFromJsonAsync<List<VanStockItem>>("/api/me/van-stock", Json.Options))!.Single().Quantity.ShouldBe(3);
    }
}
