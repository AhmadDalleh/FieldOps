using System.Security.Claims;
using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Assets;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Inventory;
using FieldOps.Application.Features.Invoices;
using FieldOps.Application.Features.Sites;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Domain.Assets;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Inventory;
using FieldOps.Domain.WorkOrders;
using FieldOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldOps.Infrastructure.Persistence;

/// <summary>
/// The Development sample company (docs/03-database.md "Seed data"): customers in Dubai and Sharjah with sites and assets,
/// stocked parts, and work orders in every status, some invoiced. It goes through the application's own handlers and domain
/// methods, acting as the seeded admin or technician, so the data obeys the same rules as real use.
/// </summary>
public sealed class DemoDataSeeder(IServiceProvider services, AppDbContext db, IHttpContextAccessor http, TimeProvider clock)
{
    private sealed record CustomerSeed(string Name, CustomerType Type, string Phone, string? Email, SiteSeed[] Sites);

    private sealed record SiteSeed(string Name, string Address, string City, double Latitude, double Longitude, AssetSeed[] Assets);

    private sealed record AssetSeed(AssetType Type, string Name, string Manufacturer, string Model, string Serial, int InstalledYearsAgo);

    private static readonly CustomerSeed[] Customers =
    [
        new("Al Noor Trading", CustomerType.Business, "+97142345678", "facilities@alnoor.example", [
            new("Head office", "Al Maktoum Road, Deira", "Dubai", 25.2667, 55.3260, [
                new(AssetType.AC, "Lobby split AC", "Daikin", "FTXM50", "DK-50-1101", 2),
                new(AssetType.AC, "Meeting room AC", "Daikin", "FTXM35", "DK-35-2210", 2)]),
            new("Warehouse", "Street 18, Al Quoz Industrial 3", "Dubai", 25.1350, 55.2290, [
                new(AssetType.Generator, "Standby generator", "Cummins", "C150D5", "CU-150-7781", 5)])]),
        new("Gulf Breeze Hotels", CustomerType.Business, "+97143216540", "engineering@gulfbreeze.example", [
            new("Gulf Breeze JBR", "The Walk, Jumeirah Beach Residence", "Dubai", 25.0780, 55.1340, [
                new(AssetType.Chiller, "Rooftop chiller 1", "Carrier", "30XA-252", "CR-252-0091", 6),
                new(AssetType.Boiler, "Hot water boiler", "Viessmann", "Vitoplex 100", "VS-100-3320", 4)])]),
        new("Marina Heights Owners Association", CustomerType.Business, "+97144567890", "om@marinaheights.example", [
            new("Tower A", "Al Marsa Street, Dubai Marina", "Dubai", 25.0800, 55.1400, [
                new(AssetType.Elevator, "Passenger lift 1", "Otis", "Gen2", "OT-G2-5512", 8),
                new(AssetType.Pump, "Booster pump set", "Grundfos", "Hydro MPC", "GF-MPC-118", 3)])]),
        new("Emirates Medical Clinic", CustomerType.Business, "+97143334455", null, [
            new("Jumeirah clinic", "Jumeirah Beach Road, Umm Suqeim", "Dubai", 25.1560, 55.2150, [
                new(AssetType.AC, "Clinic ducted AC", "Carrier", "42QSS", "CR-42-7710", 1)])]),
        new("Fatima Al Hashimi", CustomerType.Individual, "+971501234567", "fatima.h@example.com", [
            new("Villa 12", "Street 4, Al Barsha 2", "Dubai", 25.1000, 55.2000, [
                new(AssetType.AC, "Majlis AC", "LG", "Dualcool", "LG-DC-4411", 3)])]),
        new("Sharjah Logistics Park", CustomerType.Business, "+97165551234", "maintenance@slp.example", [
            new("Main yard", "Industrial Area 13", "Sharjah", 25.3100, 55.4200, [
                new(AssetType.Electrical, "Main distribution board", "Schneider", "Prisma P", "SC-PP-9031", 7),
                new(AssetType.Generator, "Yard generator", "Perkins", "2206A", "PK-2206-114", 9)])]),
        new("Desert Rose Restaurant", CustomerType.Business, "+97142229988", null, [
            new("City Walk", "City Walk, Al Wasl", "Dubai", 25.2070, 55.2620, [
                new(AssetType.AC, "Kitchen extract AC", "Trane", "Odyssey", "TR-OD-2231", 2)])]),
        new("Blue Wave Fitness", CustomerType.Business, "+97143001122", "ops@bluewave.example", [
            new("Business Bay gym", "Marasi Drive, Business Bay", "Dubai", 25.1850, 55.2750, [
                new(AssetType.Plumbing, "Shower water heater", "Ariston", "Pro1 Eco", "AR-PE-6620", 1)])]),
        new("Al Qasba Offices", CustomerType.Business, "+97165567788", null, [
            new("Qasba tower", "Al Qasba Canal", "Sharjah", 25.3290, 55.3830, [
                new(AssetType.Chiller, "Office chiller", "York", "YVAA", "YK-YV-8810", 5)])]),
        new("Oasis International School", CustomerType.Business, "+97165009911", "facilities@oasis.example", [
            new("Muwaileh campus", "University City Road, Muwaileh", "Sharjah", 25.2900, 55.4700, [
                new(AssetType.AC, "Classroom block VRF", "Mitsubishi", "City Multi", "MI-CM-3301", 4)]),
            new("Sports hall", "University City Road, Muwaileh", "Sharjah", 25.2910, 55.4720, [])]),
    ];

    private sealed record PartSeed(string Sku, string Name, PartUnit Unit, decimal Cost, decimal Price, decimal Reorder, decimal Warehouse, decimal PerVan);

    private static readonly PartSeed[] Parts =
    [
        new("CAP-35", "Run capacitor 35 µF", PartUnit.Pcs, 9m, 18.75m, 10, 40, 4),
        new("CAP-45", "Run capacitor 45 µF", PartUnit.Pcs, 11m, 22m, 10, 30, 3),
        new("FLT-2020", "Air filter 20x20", PartUnit.Pcs, 12m, 25m, 20, 60, 6),
        new("FLT-1625", "Air filter 16x25", PartUnit.Pcs, 11m, 23m, 20, 12, 0),
        new("R410A", "Refrigerant R410A", PartUnit.Kg, 30m, 65m, 15, 50, 3),
        new("R32", "Refrigerant R32", PartUnit.Kg, 28m, 60m, 10, 25, 2),
        new("CON-24", "Contactor 24 V", PartUnit.Pcs, 35m, 70m, 5, 15, 1),
        new("TH-DIG", "Digital thermostat", PartUnit.Pcs, 120m, 240m, 5, 8, 0),
        new("FAN-M1", "Condenser fan motor", PartUnit.Pcs, 260m, 480m, 3, 5, 0),
        new("BLT-A42", "V-belt A42", PartUnit.Pcs, 18m, 38m, 10, 20, 2),
        new("DRN-TAB", "Drain pan tablets (pack)", PartUnit.Pcs, 8m, 18m, 20, 45, 5),
        new("PIPE-CU14", "Copper pipe 1/4\"", PartUnit.M, 14m, 30m, 30, 120, 10),
        new("PIPE-CU38", "Copper pipe 3/8\"", PartUnit.M, 18m, 38m, 30, 18, 5),
        new("INS-TAPE", "Insulation tape", PartUnit.Pcs, 4m, 10m, 25, 80, 8),
        new("BRK-32A", "Circuit breaker 32 A", PartUnit.Pcs, 45m, 90m, 8, 14, 1),
        new("CBL-6MM", "Cable 6 mm²", PartUnit.M, 7m, 15m, 50, 200, 20),
        new("VLV-BALL", "Ball valve 1\"", PartUnit.Pcs, 25m, 55m, 6, 10, 1),
        new("PMP-SEAL", "Pump mechanical seal", PartUnit.Pcs, 95m, 190m, 4, 3, 0),
        new("OIL-GEN", "Generator oil 15W-40", PartUnit.L, 16m, 32m, 40, 100, 0),
        new("FLT-FUEL", "Generator fuel filter", PartUnit.Pcs, 38m, 80m, 6, 9, 1),
    ];

    // A valid 1×1 PNG, standing in for a customer's signature.
    private static readonly byte[] SignaturePng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private Guid _adminId;
    private readonly Dictionary<string, (Guid UserId, Guid TechnicianId)> _technicians = [];
    private readonly List<(Guid CustomerId, Guid SiteId, Guid? AssetId)> _places = [];
    private readonly Dictionary<string, Guid> _parts = [];

    private T Get<T>() where T : notnull => services.GetRequiredService<T>();

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Customers.AnyAsync(ct)) return;

        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Email!, u => u.Id, ct);
        _adminId = users["admin@fieldops.local"];
        foreach (var email in new[] { "tech1@fieldops.local", "tech2@fieldops.local", "tech3@fieldops.local" })
        {
            var userId = users[email];
            _technicians[email] = (userId, await db.Technicians.Where(t => t.UserId == userId).Select(t => t.Id).SingleAsync(ct));
        }

        var previous = http.HttpContext;
        try
        {
            ActAsAdmin();
            await SeedCustomersAsync(ct);
            await SeedPartsAsync(ct);
            await SeedWorkOrdersAsync(ct);
        }
        finally
        {
            http.HttpContext = previous;
        }
    }

    private void ActAs(Guid userId, Role role, Guid? technicianId = null)
    {
        var claims = new List<Claim> { new(ClaimNames.Subject, userId.ToString()), new(ClaimNames.Role, role.ToString()) };
        if (technicianId is { } id) claims.Add(new Claim(ClaimNames.TechnicianId, id.ToString()));
        http.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Seed")) };
    }

    private void ActAsAdmin() => ActAs(_adminId, Role.Admin);

    private static T Ok<T>(Result<T> result, string what) =>
        result.IsSuccess ? result.Value : throw new InvalidOperationException($"Seeding {what} failed: {result.Error.Message}");

    private static void Ok(Result result, string what)
    {
        if (result.IsFailure) throw new InvalidOperationException($"Seeding {what} failed: {result.Error.Message}");
    }

    private async Task SeedCustomersAsync(CancellationToken ct)
    {
        var today = clock.Today();
        foreach (var c in Customers)
        {
            var customer = Ok(await Get<CreateCustomerHandler>().Handle(new CreateCustomerCommand(
                new CustomerInput(c.Name, c.Type, c.Email, c.Phone, null, null, null), Force: true), ct), c.Name);
            foreach (var s in c.Sites)
            {
                var site = Ok(await Get<AddSiteHandler>().Handle(new AddSiteCommand(customer.Id,
                    new SiteInput(s.Name, s.Address, null, s.City, null, "AE", s.Latitude, s.Longitude, null)), ct), s.Name);
                if (s.Assets.Length == 0) _places.Add((customer.Id, site.Id, null));
                foreach (var a in s.Assets)
                {
                    var installed = today.AddYears(-a.InstalledYearsAgo);
                    var asset = Ok(await Get<RegisterAssetHandler>().Handle(new RegisterAssetCommand(site.Id, new AssetInput(
                        a.Type, a.Name, a.Manufacturer, a.Model, a.Serial, installed, installed.AddYears(3))), ct), a.Name);
                    _places.Add((customer.Id, site.Id, asset.Id));
                }
            }
        }
    }

    private async Task SeedPartsAsync(CancellationToken ct)
    {
        var vans = await db.StockLocations.AsNoTracking()
            .Where(l => l.TechnicianId != null)
            .Select(l => l.Id)
            .ToListAsync(ct);
        foreach (var p in Parts)
        {
            var part = Ok(await Get<CreatePartHandler>().Handle(new CreatePartCommand(
                new PartInput(p.Sku, p.Name, null, p.Unit, p.Cost, p.Price, p.Reorder)), ct), p.Sku);
            _parts[p.Sku] = part.Id;
            Ok(await Get<ReceiveStockHandler>().Handle(new ReceiveStockCommand(
                new ReceiveInput(part.Id, StockLocation.MainWarehouseId, p.Warehouse + p.PerVan * vans.Count)), ct), p.Sku);
            if (p.PerVan == 0) continue;
            foreach (var van in vans)
                Ok(await Get<TransferStockHandler>().Handle(new TransferStockCommand(
                    new TransferInput(part.Id, StockLocation.MainWarehouseId, van, p.PerVan)), ct), p.Sku);
        }
    }

    private sealed record JobSeed(
        int Place, string Title, WorkOrderType Type, WorkOrderPriority Priority, WorkOrderStatus Status, string? Technician = null,
        double StartHour = 9, int DayOffset = 0, (string Sku, decimal Quantity)[]? Parts = null, bool Paid = false);

    private async Task SeedWorkOrdersAsync(CancellationToken ct)
    {
        const string one = "tech1@fieldops.local", two = "tech2@fieldops.local", three = "tech3@fieldops.local";
        JobSeed[] jobs =
        [
            new(5, "Lift stuck between floors", WorkOrderType.Repair, WorkOrderPriority.Urgent, WorkOrderStatus.New),
            new(6, "Booster pump pressure low", WorkOrderType.Repair, WorkOrderPriority.High, WorkOrderStatus.New),
            new(15, "Inspect sports hall ventilation", WorkOrderType.Inspection, WorkOrderPriority.Low, WorkOrderStatus.New),
            new(10, "Generator annual service", WorkOrderType.Maintenance, WorkOrderPriority.Medium, WorkOrderStatus.New),
            new(0, "Quarterly AC service (lobby)", WorkOrderType.Maintenance, WorkOrderPriority.Medium, WorkOrderStatus.Scheduled, one, 14),
            new(8, "Majlis AC making noise", WorkOrderType.Repair, WorkOrderPriority.Medium, WorkOrderStatus.Scheduled, three, 10, 1),
            new(3, "Chiller alarm: high head pressure", WorkOrderType.Repair, WorkOrderPriority.High, WorkOrderStatus.Dispatched, two, 16),
            new(7, "Clinic AC not cooling", WorkOrderType.Repair, WorkOrderPriority.High, WorkOrderStatus.EnRoute, one, 11),
            new(11, "Kitchen extract AC leaking", WorkOrderType.Repair, WorkOrderPriority.Medium, WorkOrderStatus.InProgress, three, 9,
                Parts: [("DRN-TAB", 1)]),
            new(12, "Water heater not heating", WorkOrderType.Repair, WorkOrderPriority.Medium, WorkOrderStatus.OnHold, two, 8,
                Parts: [("VLV-BALL", 1)]),
            new(1, "Meeting room AC not cooling", WorkOrderType.Repair, WorkOrderPriority.High, WorkOrderStatus.Completed, one, 9, -1,
                [("CAP-35", 1), ("R410A", 1.5m)]),
            new(9, "Distribution board tripping", WorkOrderType.Repair, WorkOrderPriority.High, WorkOrderStatus.Completed, two, 8,
                Parts: [("BRK-32A", 1)]),
            new(2, "Standby generator will not start", WorkOrderType.Repair, WorkOrderPriority.Urgent, WorkOrderStatus.Invoiced, three, 9,
                -3, [("FLT-FUEL", 1)]),
            new(13, "Office chiller low flow", WorkOrderType.Repair, WorkOrderPriority.Medium, WorkOrderStatus.Invoiced, one, 10, -7,
                [("CAP-45", 1)], Paid: true),
            new(0, "Replace lobby thermostat", WorkOrderType.Installation, WorkOrderPriority.Low, WorkOrderStatus.Cancelled),
        ];

        var today = clock.Today();
        var now = clock.GetUtcNow();
        foreach (var job in jobs)
        {
            ActAsAdmin();
            var (customerId, siteId, assetId) = _places[job.Place];
            var dayStart = BusinessCalendar.DayRange(today.AddDays(job.DayOffset)).From;
            var start = dayStart.AddHours(job.StartHour);
            DateTimeOffset? dueBy = job.Status == WorkOrderStatus.New
                ? job.Priority == WorkOrderPriority.Urgent ? now.AddHours(4) : now.AddDays(3)
                : start.AddHours(8);
            var created = Ok(await Get<CreateWorkOrderHandler>().Handle(new CreateWorkOrderCommand(new CreateWorkOrderInput(
                customerId, siteId, assetId, job.Title, null, job.Type, job.Priority, dueBy)), ct), job.Title);

            if (job.Status == WorkOrderStatus.Cancelled)
                await StepAsync(created.Id, (w, t) => w.Cancel("Customer bought the thermostat themselves", _adminId, t), now, ct);
            if (job.Technician is null) continue;

            var tech = _technicians[job.Technician];
            // Jobs still to happen stay where they are; jobs already under way or done started in the past.
            var begin = job.Status is WorkOrderStatus.Scheduled or WorkOrderStatus.Dispatched
                ? start
                : job.DayOffset == 0 ? Min(start, now.AddHours(-2)) : start;
            var officeTime = Min(begin.AddHours(-2), now);
            await StepAsync(created.Id, (w, t) => w.Schedule(tech.TechnicianId, begin, begin.AddHours(2), _adminId, t), officeTime, ct);
            if (job.Status == WorkOrderStatus.Scheduled) continue;
            await StepAsync(created.Id, (w, t) => w.Dispatch(_adminId, t), officeTime.AddMinutes(5), ct);
            if (job.Status == WorkOrderStatus.Dispatched) continue;

            await StepAsync(created.Id, (w, t) => w.EnRoute(tech.UserId, t), begin.AddMinutes(-25), ct);
            if (job.Status == WorkOrderStatus.EnRoute) continue;
            await StepAsync(created.Id, (w, t) => w.Start(tech.UserId, t), begin, ct);

            ActAs(tech.UserId, Role.Technician, tech.TechnicianId);
            foreach (var (sku, quantity) in job.Parts ?? [])
                Ok(await Get<AddWorkOrderPartHandler>().Handle(new AddWorkOrderPartCommand(created.Id,
                    new UsePartInput(_parts[sku], quantity)), ct), sku);
            if (job.Status == WorkOrderStatus.InProgress) continue;
            if (job.Status == WorkOrderStatus.OnHold)
            {
                await StepAsync(created.Id, (w, t) => w.Hold("Waiting for a replacement heating element", tech.UserId, t),
                    begin.AddMinutes(40), ct);
                continue;
            }

            var finished = begin.AddMinutes(95);
            var signature = await SignatureAsync(created.Id, tech.UserId, finished, ct);
            await StepAsync(created.Id, (w, t) => w.Complete("Diagnosed and fixed the fault; tested with the customer.", "Site manager",
                signature, tech.UserId, t, "Not needed for this job"), finished, ct);
            if (job.Status == WorkOrderStatus.Completed) continue;

            ActAsAdmin();
            var invoice = Ok(await Get<GenerateInvoiceHandler>().Handle(new GenerateInvoiceCommand(created.Id), ct), "invoice");
            Ok(await Get<IssueInvoiceHandler>().Handle(new IssueInvoiceCommand(invoice.Id), ct), "invoice issue");
            if (job.Paid)
                Ok(await Get<MarkInvoicePaidHandler>().Handle(new MarkInvoicePaidCommand(invoice.Id,
                    new MarkPaidInput(today, "TT-240118")), ct), "payment");
        }
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    /// <summary>Applies one state-machine step at <paramref name="at"/>, as the field or office user would have.</summary>
    private async Task StepAsync(Guid workOrderId, Func<WorkOrder, DateTimeOffset, Result> step, DateTimeOffset at, CancellationToken ct)
    {
        var workOrder = await db.WorkOrders.Include(w => w.Tasks).Include(w => w.TimeEntries).SingleAsync(w => w.Id == workOrderId, ct);
        Ok(step(workOrder, at), $"{workOrder.Number} step");
        await db.SaveChangesAsync(ct);
    }

    private async Task<Guid> SignatureAsync(Guid workOrderId, Guid userId, DateTimeOffset at, CancellationToken ct)
    {
        var workOrder = await db.WorkOrders.AsNoTracking().SingleAsync(w => w.Id == workOrderId, ct);
        var signature = Ok(Attachment.Create(workOrder, AttachmentKind.Signature, "signature.png", "image/png", SignaturePng.Length,
            userId, at), "signature");
        await using (var bytes = new MemoryStream(SignaturePng))
            await Get<IFileStorage>().SaveAsync(bytes, signature.StorageKey, ct);
        db.Attachments.Add(signature);
        await db.SaveChangesAsync(ct);
        return signature.Id;
    }
}
