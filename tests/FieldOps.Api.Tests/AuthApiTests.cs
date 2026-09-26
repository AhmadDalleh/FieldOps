using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Features.Auth;
using FieldOps.Domain.Identity;
using Shouldly;

namespace FieldOps.Api.Tests;

public class AuthApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task<AuthResponse> LoginAs(Role role)
    {
        var user = await Factory.CreateUserAsync(role);
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = ApiFactory.Password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
    }

    [Fact]
    public async Task Login_returns_tokens_and_the_users_role()
    {
        var auth = await LoginAs(Role.Technician);

        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        auth.User.Role.ShouldBe(Role.Technician);
        auth.User.TechnicianId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Bad_credentials_return_401_problem_details_with_a_generic_message()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "x@fieldops.test", password = "Wrong123!" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe("Auth.InvalidCredentials");
        problem.GetProperty("title").GetString().ShouldBe("The email or password is incorrect.");
    }

    [Fact]
    public async Task Empty_login_returns_400_with_field_errors()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "", password = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("errors").TryGetProperty("Email", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_rejects_reuse()
    {
        var auth = await LoginAs(Role.Dispatcher);
        var client = Factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var auth = await LoginAs(Role.Dispatcher);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = auth.RefreshToken });
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/auth/me", "GET")]
    [InlineData("/api/auth/logout", "POST")]
    [InlineData("/api/auth/change-password", "POST")]
    public async Task Anonymous_requests_to_protected_auth_endpoints_return_401(string url, string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST") request.Content = JsonContent.Create(new { });

        var response = await Factory.CreateClient().SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Dispatcher)]
    [InlineData(Role.Technician)]
    public async Task Any_role_can_read_me_and_change_their_password(Role role)
    {
        var client = await Factory.CreateClientAs(role);

        var me = await client.GetFromJsonAsync<AuthUserDto>("/api/auth/me", Json.Options);
        var change = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = ApiFactory.Password, newPassword = "NewPass456" });

        me!.Role.ShouldBe(role);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Weak_new_password_returns_400()
    {
        var client = await Factory.CreateClientAs(Role.Dispatcher);

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = ApiFactory.Password, newPassword = "weak" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Health_check_is_public_and_healthy()
    {
        var response = await Factory.CreateClient().GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
