using System.Net;
using System.Net.Http.Json;
using FieldOps.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Shouldly;

namespace FieldOps.Api.Tests;

public class SecurityTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Repeated_sign_in_attempts_are_slowed_down()
    {
        using var limited = Factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Auth:PermitLimit", "3"));
        var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
            (await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@fieldops.test", password = "wrong-password" }))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var blocked = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@fieldops.test", password = "wrong-password" });
        blocked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        blocked.Headers.RetryAfter.ShouldNotBeNull();
        (await blocked.Content.ReadAsStringAsync()).ShouldContain("Too many attempts");
        (await client.GetAsync("/health")).StatusCode.ShouldBe(HttpStatusCode.OK); // only sign-in is limited
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await Factory.CreateClient().GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("frame-ancestors 'none'");
        response.Headers.GetValues("Permissions-Policy").Single().ShouldContain("geolocation=(self)");
    }
}
