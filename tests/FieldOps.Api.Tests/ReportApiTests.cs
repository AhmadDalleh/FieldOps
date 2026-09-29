using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Domain.Identity;
using Shouldly;

namespace FieldOps.Api.Tests;

public class ReportApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Office_staff_see_the_dashboard_and_technicians_do_not()
    {
        var dispatcher = await Factory.CreateClientAs(Role.Dispatcher);
        var admin = await Factory.CreateClientAs(Role.Admin);
        var tech = await Factory.CreateClientAs(Role.Technician);

        var dashboard = await dispatcher.GetFromJsonAsync<JsonElement>("/api/dashboard/today");
        dashboard.GetProperty("openByStatus")[0].GetProperty("status").GetString().ShouldBe("New");
        dashboard.GetProperty("technicians")[0].GetProperty("state").GetString().ShouldBe("Free");
        (await admin.GetAsync("/api/dashboard/today")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await tech.GetAsync("/api/dashboard/today")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Factory.CreateClient().GetAsync("/api/dashboard/today")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/reports/technicians")]
    [InlineData("/api/reports/revenue")]
    [InlineData("/api/reports/parts-usage")]
    public async Task Reports_are_for_admins_only(string url)
    {
        var admin = await Factory.CreateClientAs(Role.Admin);
        (await admin.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await Factory.CreateClientAs(Role.Dispatcher)).GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await (await Factory.CreateClientAs(Role.Technician)).GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reports_download_as_csv_and_reject_bad_ranges_and_options()
    {
        var admin = await Factory.CreateClientAs(Role.Admin);

        var csv = await admin.GetAsync("/api/reports/revenue?from=2026-01-01&to=2026-03-31&groupBy=customer&format=csv");
        csv.StatusCode.ShouldBe(HttpStatusCode.OK);
        csv.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        csv.Content.Headers.ContentDisposition!.FileName.ShouldBe("revenue-by-customer-2026-01-01-to-2026-03-31.csv");
        (await csv.Content.ReadAsStringAsync()).TrimStart('﻿').ShouldStartWith("Customer,Invoices,Subtotal");

        var json = await admin.GetFromJsonAsync<JsonElement>("/api/reports/technicians?from=2026-01-01&to=2026-01-31");
        json.GetProperty("from").GetString().ShouldBe("2026-01-01");

        (await admin.GetAsync("/api/reports/parts-usage?from=2026-02-01&to=2026-01-01")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/reports/technicians?from=2025-01-01&to=2026-06-01")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/reports/revenue?groupBy=week")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/reports/revenue?format=xlsx")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
