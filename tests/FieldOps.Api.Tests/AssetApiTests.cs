using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Features.Assets;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Sites;
using FieldOps.Domain.Identity;
using Shouldly;

namespace FieldOps.Api.Tests;

public class AssetApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static object NewAsset(string serial = "SN-1") =>
        new { assetType = "AC", name = "Split AC", manufacturer = "Daikin", serialNumber = serial, warrantyExpiresOn = "2099-01-31" };

    private async Task<(Guid CustomerId, Guid SiteId, AssetDto Asset)> GivenAsset()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        var customer = await (await admin.PostAsJsonAsync("/api/customers", new { name = "Acme", phone = "+971500000000", type = "Business" }))
            .Content.ReadFromJsonAsync<CustomerDto>(Json.Options);
        var site = await (await admin.PostAsJsonAsync($"/api/customers/{customer!.Id}/sites", new { name = "HQ", addressLine1 = "Road", city = "Dubai" }))
            .Content.ReadFromJsonAsync<SiteDto>(Json.Options);
        var asset = await (await admin.PostAsJsonAsync($"/api/sites/{site!.Id}/assets", NewAsset()))
            .Content.ReadFromJsonAsync<AssetDto>(Json.Options);
        return (customer.Id, site.Id, asset!);
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Dispatcher)]
    public async Task Office_staff_can_register_view_edit_and_list_assets(Role role)
    {
        var (customerId, siteId, asset) = await GivenAsset();
        var client = await Factory.CreateClientAs(role);

        var created = await client.PostAsJsonAsync($"/api/sites/{siteId}/assets", NewAsset("SN-2"));
        var responses = new[]
        {
            await client.GetAsync($"/api/sites/{siteId}/assets"),
            await client.GetAsync($"/api/customers/{customerId}/assets"),
            await client.GetAsync($"/api/assets/{asset.Id}"),
            await client.PutAsJsonAsync($"/api/assets/{asset.Id}", NewAsset()),
            await client.GetAsync($"/api/assets/{asset.Id}/history"),
        };

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task Technician_is_forbidden_from_asset_endpoints_without_an_assigned_job()
    {
        var (customerId, siteId, asset) = await GivenAsset();
        var client = await Factory.CreateClientAs(Role.Technician);

        var responses = new[]
        {
            await client.PostAsJsonAsync($"/api/sites/{siteId}/assets", NewAsset("SN-2")),
            await client.GetAsync($"/api/sites/{siteId}/assets"),
            await client.GetAsync($"/api/customers/{customerId}/assets"),
            await client.GetAsync($"/api/assets/{asset.Id}"),
            await client.PutAsJsonAsync($"/api/assets/{asset.Id}", NewAsset()),
            await client.GetAsync($"/api/assets/{asset.Id}/history"),
        };

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_request_returns_401()
    {
        (await Factory.CreateClient().GetAsync($"/api/assets/{Guid.CreateVersion7()}/history")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Duplicate_serial_returns_400_with_its_code()
    {
        var (_, siteId, _) = await GivenAsset();
        var client = await Factory.CreateClientAs(Role.Dispatcher);

        var response = await client.PostAsJsonAsync($"/api/sites/{siteId}/assets", NewAsset("SN-1"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().ShouldBe("Asset.DuplicateSerial");
    }

    [Fact]
    public async Task Asset_json_carries_dates_and_the_warranty_flag()
    {
        var (_, _, asset) = await GivenAsset();

        asset.WarrantyExpiresOn.ShouldBe(new DateOnly(2099, 1, 31));
        asset.UnderWarranty.ShouldBeTrue();
    }
}
