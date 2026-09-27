using System.Net.Http.Json;
using FieldOps.Api.Tests.Infrastructure;
using FieldOps.Application.Features.Dashboard;
using FieldOps.Domain.Invoicing;
using FieldOps.Domain.WorkOrders;
using FieldOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace FieldOps.Api.Tests;

public class SeedDataTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task Seed()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevSeeder>().SeedAsync(default);
    }

    [Fact]
    public async Task The_development_seed_builds_a_sample_company_once_and_its_users_can_sign_in()
    {
        await Seed();
        await Seed(); // safe to run on every start

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.CountAsync()).ShouldBe(5);
        (await db.Technicians.CountAsync()).ShouldBe(3);
        (await db.StockLocations.CountAsync()).ShouldBe(4); // the warehouse and three vans
        (await db.Skills.CountAsync()).ShouldBe(4);
        (await db.Customers.CountAsync()).ShouldBe(10);
        (await db.Sites.Select(s => s.City).Distinct().OrderBy(c => c).ToListAsync()).ShouldBe(["Dubai", "Sharjah"]);
        (await db.Sites.AllAsync(s => s.Latitude != null && s.Longitude != null)).ShouldBeTrue();
        (await db.Assets.CountAsync()).ShouldBe(15);
        (await db.Parts.CountAsync()).ShouldBe(20);
        (await db.StockLevels.AnyAsync(l => l.Quantity > 0)).ShouldBeTrue();
        (await db.WorkOrders.CountAsync()).ShouldBe(15);
        (await db.WorkOrders.Select(w => w.Status).Distinct().CountAsync()).ShouldBe(Enum.GetValues<WorkOrderStatus>().Length);
        (await db.Invoices.Select(i => i.Status).OrderBy(s => s).ToListAsync()).ShouldBe([InvoiceStatus.Issued, InvoiceStatus.Paid]);
        var settings = await db.AppSettings.SingleAsync();
        (settings.VatRate, settings.LaborRatePerHour, settings.Currency).ShouldBe((5.00m, 150m, "AED"));

        var admin = await Factory.LoginAs("admin@fieldops.local", DevSeeder.Password);
        var dashboard = await admin.GetFromJsonAsync<DashboardDto>("/api/dashboard/today", Json.Options);
        dashboard!.Unassigned.ShouldBe(4);
        dashboard.UrgentUnassigned.ShouldHaveSingleItem().Title.ShouldBe("Lift stuck between floors");
        (await (await Factory.LoginAs("tech2@fieldops.local", DevSeeder.Password)).GetAsync("/api/me/jobs")).EnsureSuccessStatusCode();
    }
}
