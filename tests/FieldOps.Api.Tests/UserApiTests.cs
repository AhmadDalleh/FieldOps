using System.Net;
using System.Net.Http.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Users;
using FieldOps.Domain.Identity;
using Shouldly;

namespace FieldOps.Api.Tests;

public class UserApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static object NewUser(string role = "Technician") =>
        new { fullName = "New Tech", email = $"new-{Guid.NewGuid():N}@fieldops.test", phoneNumber = "+971500000001", role, temporaryPassword = "Temp1234" };

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Dispatcher, HttpStatusCode.Forbidden)]
    [InlineData(Role.Technician, HttpStatusCode.Forbidden)]
    public async Task List_users_authorization(Role role, HttpStatusCode expected)
    {
        var client = await Factory.CreateClientAs(role);

        var response = await client.GetAsync("/api/users");

        response.StatusCode.ShouldBe(expected);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.Created)]
    [InlineData(Role.Dispatcher, HttpStatusCode.Forbidden)]
    [InlineData(Role.Technician, HttpStatusCode.Forbidden)]
    public async Task Create_user_authorization(Role role, HttpStatusCode expected)
    {
        var client = await Factory.CreateClientAs(role);

        var response = await client.PostAsJsonAsync("/api/users", NewUser());

        response.StatusCode.ShouldBe(expected);
    }

    [Theory]
    [InlineData(Role.Dispatcher)]
    [InlineData(Role.Technician)]
    public async Task Update_and_deactivate_are_forbidden_for_non_admins(Role role)
    {
        var target = await Factory.CreateUserAsync(Role.Dispatcher);
        var client = await Factory.CreateClientAs(role);

        var update = await client.PutAsJsonAsync($"/api/users/{target.Id}", new { fullName = "X", email = target.Email, role = "Dispatcher" });
        var deactivate = await client.PostAsync($"/api/users/{target.Id}/deactivate", null);
        var activate = await client.PostAsync($"/api/users/{target.Id}/activate", null);

        update.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        deactivate.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        activate.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_request_returns_401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/users");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_creates_edits_and_deactivates_a_user()
    {
        var client = await Factory.CreateClientAs(Role.Admin);

        var created = await (await client.PostAsJsonAsync("/api/users", NewUser("Dispatcher"))).Content.ReadFromJsonAsync<UserDto>(Json.Options);
        var updated = await client.PutAsJsonAsync($"/api/users/{created!.Id}", new { fullName = "Renamed", email = created.Email, phoneNumber = (string?)null, role = "Dispatcher" });
        var deactivated = await client.PostAsync($"/api/users/{created.Id}/deactivate", null);
        var fetched = await client.GetFromJsonAsync<UserDto>($"/api/users/{created.Id}", Json.Options);
        var login = await Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = created.Email, password = "Temp1234" });

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        deactivated.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        fetched!.FullName.ShouldBe("Renamed");
        fetched.IsActive.ShouldBeFalse();
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_is_paged()
    {
        var client = await Factory.CreateClientAs(Role.Admin);
        await Factory.CreateUserAsync(Role.Dispatcher);
        await Factory.CreateUserAsync(Role.Technician);

        var page = await client.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?page=1&pageSize=2", Json.Options);

        page!.TotalCount.ShouldBe(3);
        page.Items.Count.ShouldBe(2);
        page.PageSize.ShouldBe(2);
    }

    [Fact]
    public async Task Invalid_user_returns_400_and_unknown_user_returns_404()
    {
        var client = await Factory.CreateClientAs(Role.Admin);

        var invalid = await client.PostAsJsonAsync("/api/users", new { fullName = "", email = "not-an-email", role = "Admin", temporaryPassword = "x" });
        var missing = await client.GetAsync($"/api/users/{Guid.CreateVersion7()}");

        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
