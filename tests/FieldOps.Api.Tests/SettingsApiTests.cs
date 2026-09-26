using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Domain.Identity;
using Shouldly;

namespace FieldOps.Api.Tests;

public class SettingsApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly object ValidSettings = new
    {
        companyName = "Cool Air LLC",
        companyAddress = "Dubai",
        trn = "100123456700003",
        vatRate = 5,
        laborRatePerHour = 150,
        invoiceDueDays = 30,
        currency = "AED",
    };

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Dispatcher, HttpStatusCode.OK)]
    [InlineData(Role.Technician, HttpStatusCode.Forbidden)]
    public async Task Get_settings_authorization(Role role, HttpStatusCode expected)
    {
        var client = await Factory.CreateClientAs(role);

        var response = await client.GetAsync("/api/settings");

        response.StatusCode.ShouldBe(expected);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Dispatcher, HttpStatusCode.Forbidden)]
    [InlineData(Role.Technician, HttpStatusCode.Forbidden)]
    public async Task Update_settings_authorization(Role role, HttpStatusCode expected)
    {
        var client = await Factory.CreateClientAs(role);

        var response = await client.PutAsJsonAsync("/api/settings", ValidSettings);

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task Vat_rate_above_100_returns_400()
    {
        var client = await Factory.CreateClientAs(Role.Admin);

        var response = await client.PutAsJsonAsync("/api/settings", new
        {
            companyName = "Cool Air LLC",
            vatRate = 101,
            laborRatePerHour = 150,
            invoiceDueDays = 30,
            currency = "AED",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(Role.Admin, "image/png", HttpStatusCode.NoContent)]
    [InlineData(Role.Admin, "text/plain", HttpStatusCode.BadRequest)]
    [InlineData(Role.Dispatcher, "image/png", HttpStatusCode.Forbidden)]
    public async Task Logo_upload(Role role, string contentType, HttpStatusCode expected)
    {
        var client = await Factory.CreateClientAs(role);
        var file = new ByteArrayContent([137, 80, 78, 71]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var form = new MultipartFormDataContent { { file, "file", "logo.png" } };

        var response = await client.PostAsync("/api/settings/logo", form);

        response.StatusCode.ShouldBe(expected);
    }
}
