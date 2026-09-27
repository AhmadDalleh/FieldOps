using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Sites;
using FieldOps.Domain.Identity;
using Shouldly;

namespace FieldOps.Api.Tests;

public class CustomerApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static object NewCustomer(string phone = "+971500000000") =>
        new { name = "Acme", phone, type = "Business" };

    private static readonly object NewSite = new
    {
        name = "HQ", addressLine1 = "Sheikh Zayed Road", city = "Dubai", latitude = 25.2, longitude = 55.27,
    };

    private static readonly object NewContact = new { name = "Sara", email = "sara@acme.ae", isPrimary = true };

    private async Task<(HttpClient Office, CustomerDto Customer, SiteDto Site, ContactDto Contact)> GivenCustomerWithSiteAndContact()
    {
        var office = await Factory.CreateClientAs(Role.Admin);
        var customer = await (await office.PostAsJsonAsync("/api/customers", NewCustomer())).Content.ReadFromJsonAsync<CustomerDto>(Json.Options);
        var site = await (await office.PostAsJsonAsync($"/api/customers/{customer!.Id}/sites", NewSite)).Content.ReadFromJsonAsync<SiteDto>(Json.Options);
        var contact = await (await office.PostAsJsonAsync($"/api/customers/{customer.Id}/contacts", NewContact)).Content.ReadFromJsonAsync<ContactDto>(Json.Options);
        return (office, customer, site!, contact!);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.Created)]
    [InlineData(Role.Dispatcher, HttpStatusCode.Created)]
    [InlineData(Role.Technician, HttpStatusCode.Forbidden)]
    public async Task Create_customer_authorization(Role role, HttpStatusCode expected)
    {
        var client = await Factory.CreateClientAs(role);

        var response = await client.PostAsJsonAsync("/api/customers", NewCustomer());

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task Anonymous_request_returns_401()
    {
        (await Factory.CreateClient().GetAsync("/api/customers")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Dispatcher)]
    public async Task Office_staff_can_use_every_customer_and_site_endpoint(Role role)
    {
        var (_, customer, site, contact) = await GivenCustomerWithSiteAndContact();
        var client = await Factory.CreateClientAs(role);

        var responses = new[]
        {
            await client.GetAsync("/api/customers?search=acme"),
            await client.GetAsync($"/api/customers/{customer.Id}"),
            await client.PutAsJsonAsync($"/api/customers/{customer.Id}", NewCustomer()),
            await client.GetAsync($"/api/customers/{customer.Id}/contacts"),
            await client.PutAsJsonAsync($"/api/customers/{customer.Id}/contacts/{contact.Id}", NewContact),
            await client.GetAsync($"/api/customers/{customer.Id}/sites"),
            await client.GetAsync($"/api/sites/{site.Id}"),
            await client.PutAsJsonAsync($"/api/sites/{site.Id}", NewSite),
            await client.PostAsync($"/api/sites/{site.Id}/deactivate", null),
            await client.DeleteAsync($"/api/customers/{customer.Id}/contacts/{contact.Id}"),
            await client.PostAsync($"/api/customers/{customer.Id}/deactivate", null),
        };

        responses.ShouldAllBe(r => r.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Technician_is_forbidden_from_every_customer_and_site_endpoint()
    {
        var (_, customer, site, contact) = await GivenCustomerWithSiteAndContact();
        var client = await Factory.CreateClientAs(Role.Technician);

        var responses = new[]
        {
            await client.GetAsync("/api/customers"),
            await client.GetAsync($"/api/customers/{customer.Id}"),
            await client.PutAsJsonAsync($"/api/customers/{customer.Id}", NewCustomer()),
            await client.PostAsync($"/api/customers/{customer.Id}/deactivate", null),
            await client.GetAsync($"/api/customers/{customer.Id}/contacts"),
            await client.PostAsJsonAsync($"/api/customers/{customer.Id}/contacts", NewContact),
            await client.PutAsJsonAsync($"/api/customers/{customer.Id}/contacts/{contact.Id}", NewContact),
            await client.DeleteAsync($"/api/customers/{customer.Id}/contacts/{contact.Id}"),
            await client.GetAsync($"/api/customers/{customer.Id}/sites"),
            await client.PostAsJsonAsync($"/api/customers/{customer.Id}/sites", NewSite),
            await client.GetAsync($"/api/sites/{site.Id}"),
            await client.PutAsJsonAsync($"/api/sites/{site.Id}", NewSite),
            await client.PostAsync($"/api/sites/{site.Id}/deactivate", null),
        };

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Duplicate_phone_returns_409_until_forced()
    {
        var client = await Factory.CreateClientAs(Role.Dispatcher);
        await client.PostAsJsonAsync("/api/customers", NewCustomer("+971501112233"));

        var warning = await client.PostAsJsonAsync("/api/customers", NewCustomer("+971501112233"));
        var forced = await client.PostAsJsonAsync("/api/customers?force=true", NewCustomer("+971501112233"));

        warning.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await warning.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().ShouldBe("Customer.DuplicatePhone");
        forced.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Invalid_input_returns_400_and_unknown_ids_return_404()
    {
        var client = await Factory.CreateClientAs(Role.Dispatcher);

        var noPhone = await client.PostAsJsonAsync("/api/customers", new { name = "Acme", phone = "", type = "Business" });
        var badLatitude = await client.PostAsJsonAsync($"/api/customers/{Guid.CreateVersion7()}/sites",
            new { name = "HQ", addressLine1 = "Road", city = "Dubai", latitude = 95, longitude = 55 });
        var missingCustomer = await client.GetAsync($"/api/customers/{Guid.CreateVersion7()}");
        var missingSite = await client.GetAsync($"/api/sites/{Guid.CreateVersion7()}");

        noPhone.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        badLatitude.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        missingCustomer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missingSite.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_hides_inactive_customers_unless_asked()
    {
        var (office, customer, _, _) = await GivenCustomerWithSiteAndContact();
        await office.PostAsync($"/api/customers/{customer.Id}/deactivate", null);

        var active = await office.GetFromJsonAsync<PagedResult<CustomerListItem>>("/api/customers", Json.Options);
        var all = await office.GetFromJsonAsync<PagedResult<CustomerListItem>>("/api/customers?includeInactive=true", Json.Options);

        active!.TotalCount.ShouldBe(0);
        all!.Items.Single().Code.ShouldBe("C-00001");
    }
}
