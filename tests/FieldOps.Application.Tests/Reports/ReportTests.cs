using System.Text;
using FieldOps.Application.Features.Invoices;
using FieldOps.Application.Features.Inventory;
using FieldOps.Application.Features.Reports;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Inventory;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Reports;

public class ReportTests(PostgresFixture fixture) : TestBase(fixture)
{
    // The fake clock starts on 2026-10-01 at 10:00 in Dubai.
    private static readonly DateOnly October1 = new(2026, 10, 1);

    private static string Text(CsvFile file) => Encoding.UTF8.GetString(file.Content).TrimStart('﻿');

    /// <summary>Starts the job, lets <paramref name="workMinutes"/> pass, and completes it.</summary>
    private async Task CompleteAfter(Guid workOrderId, Guid technicianId, int workMinutes)
    {
        await Assign(workOrderId, technicianId);
        await Advance(workOrderId, (w, u, now) => w.Dispatch(u, now));
        await Advance(workOrderId, (w, u, now) => w.Start(u, now));
        Fixture.Clock.Advance(TimeSpan.FromMinutes(workMinutes));
        var signature = await GivenSignature(workOrderId);
        await Advance(workOrderId, (w, u, now) => w.Complete("Done", "Sara M.", signature, u, now, "Not needed"));
    }

    [Fact]
    public void A_period_defaults_to_the_month_so_far_and_must_be_in_order_and_at_most_a_year()
    {
        Fixture.Clock.Advance(TimeSpan.FromDays(14));

        ReportPeriod.Resolve(null, null, Fixture.Clock).Value.ShouldBe(new ReportPeriod(October1, new DateOnly(2026, 10, 15)));
        ReportPeriod.Resolve(null, new DateOnly(2026, 9, 20), Fixture.Clock).Value.From.ShouldBe(new DateOnly(2026, 9, 1));
        ReportPeriod.Resolve(October1, October1.AddDays(-1), Fixture.Clock).Error.ShouldBe(ReportErrors.FromAfterTo);
        ReportPeriod.Resolve(October1, October1.AddDays(365), Fixture.Clock).IsSuccess.ShouldBeTrue();
        ReportPeriod.Resolve(October1, October1.AddDays(366), Fixture.Clock).Error.ShouldBe(ReportErrors.RangeTooLong);
    }

    [Fact]
    public async Task Technician_report_counts_completed_jobs_average_duration_and_work_hours_in_the_period()
    {
        await SignInOffice(Role.Admin);
        var (customer, site) = await GivenCustomerAndSite();
        var busy = await GivenTechnician();
        var idle = await GivenTechnician();

        await CompleteAfter((await GivenWorkOrder(customer, site)).Id, busy.TechnicianId, 70);
        await CompleteAfter((await GivenWorkOrder(customer, site)).Id, busy.TechnicianId, 50);
        var cancelled = await GivenWorkOrder(customer, site);
        await Assign(cancelled.Id, busy.TechnicianId);
        await Advance(cancelled.Id, (w, u, now) => w.Cancel("Customer called off", u, now));
        Fixture.Clock.Advance(TimeSpan.FromDays(1));
        await CompleteAfter((await GivenWorkOrder(customer, site)).Id, idle.TechnicianId, 30); // on 2 October

        var (period, report) = (await Resolve<TechnicianReportHandler>().Handle(new TechnicianReportQuery(October1, October1), default)).Value;

        var top = report.Rows[0];
        (top.TechnicianId, top.CompletedJobs, top.AverageMinutes, top.WorkHours).ShouldBe((busy.TechnicianId, 2, 60, 2.00m));
        var other = report.Rows.Single(r => r.TechnicianId == idle.TechnicianId);
        (other.CompletedJobs, other.AverageMinutes, other.WorkHours).ShouldBe((0, (int?)null, 0m));
        (report.CompletedJobs, report.AverageMinutes, report.WorkHours).ShouldBe((2, 60, 2.00m));

        var csv = report.ToCsv(period);
        csv.FileName.ShouldBe("technicians-2026-10-01-to-2026-10-01.csv");
        var lines = Text(csv).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].ShouldBe("Technician,Employee code,Completed jobs,Average duration (min),Work hours");
        lines[1].ShouldEndWith(",2,60,2.00");
        lines[^1].ShouldBe("Total,,2,60,2.00");
    }

    private async Task SetLaborRate(decimal rate)
    {
        var db = NewDb();
        var settings = await db.AppSettings.SingleAsync();
        settings.Update("FieldOps Services", "Dubai", "100000000000003", 5m, rate, 30, "AED").IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync();
    }

    /// <summary>A completed job for <paramref name="customer"/> invoiced with one fee line; issued unless left as a draft.</summary>
    private async Task<Guid> Invoice((Guid Customer, Guid Site) customer, Guid technicianId, decimal fee, bool issue = true)
    {
        var wo = await GivenWorkOrder(customer.Customer, customer.Site);
        await CompleteAfter(wo.Id, technicianId, 0);
        var invoice = (await Resolve<GenerateInvoiceHandler>().Handle(new GenerateInvoiceCommand(wo.Id), default)).Value;
        (await Resolve<AddInvoiceLineHandler>().Handle(new AddInvoiceLineCommand(invoice.Id,
            new InvoiceLineInput(Domain.Invoicing.InvoiceLineType.Other, "Call-out fee", 1, fee)), default)).IsSuccess.ShouldBeTrue();
        if (issue) (await Resolve<IssueInvoiceHandler>().Handle(new IssueInvoiceCommand(invoice.Id), default)).IsSuccess.ShouldBeTrue();
        return invoice.Id;
    }

    [Fact]
    public async Task Revenue_report_totals_issued_and_paid_invoices_by_month_or_customer()
    {
        await SignInOffice(Role.Admin);
        await SetLaborRate(0);
        var acme = await GivenCustomerAndSite("Acme");
        var zen = await GivenCustomerAndSite("Zen, \"Towers\"");
        var tech = await GivenTechnician();

        var paid = await Invoice(acme, tech.TechnicianId, 100m);                  // 105.00, paid
        (await Resolve<MarkInvoicePaidHandler>().Handle(new MarkInvoicePaidCommand(paid, new MarkPaidInput(October1, "TT-1")), default))
            .IsSuccess.ShouldBeTrue();
        await Invoice(zen, tech.TechnicianId, 200m);                               // 210.00, outstanding
        var voided = await Invoice(acme, tech.TechnicianId, 999m);
        (await Resolve<VoidInvoiceHandler>().Handle(new VoidInvoiceCommand(voided, new VoidInvoiceInput("Wrong")), default))
            .IsSuccess.ShouldBeTrue();
        await Invoice(acme, tech.TechnicianId, 500m, issue: false);                // a draft is not revenue
        Fixture.Clock.Advance(TimeSpan.FromDays(31));
        await Invoice(acme, tech.TechnicianId, 40m);                               // 42.00 in November

        var handler = Resolve<RevenueReportHandler>();
        var (period, byMonth) = (await handler.Handle(new RevenueReportQuery(October1, new DateOnly(2026, 11, 30)), default)).Value;

        byMonth.Rows.Select(r => (r.Key, r.Label, r.Invoices, r.Subtotal, r.Vat, r.Total, r.Paid, r.Outstanding)).ShouldBe([
            ("2026-10", "October 2026", 2, 300m, 15m, 315m, 105m, 210m),
            ("2026-11", "November 2026", 1, 40m, 2m, 42m, 0m, 42m),
        ]);
        (byMonth.Totals.Invoices, byMonth.Totals.Total, byMonth.Totals.Outstanding).ShouldBe((3, 357m, 252m));

        var (_, byCustomer) = (await handler.Handle(
            new RevenueReportQuery(October1, new DateOnly(2026, 11, 30), RevenueGrouping.Customer), default)).Value;
        byCustomer.Rows.Select(r => (r.Label, r.Total)).ShouldBe([("Zen, \"Towers\"", 210m), ("Acme", 147m)]);

        var csv = Text(byCustomer.ToCsv(period)).Split("\r\n");
        csv[0].ShouldBe("Customer,Invoices,Subtotal,VAT,Total,Paid,Outstanding");
        csv[1].ShouldBe("\"Zen, \"\"Towers\"\"\",1,200.00,10.00,210.00,0.00,210.00");
        csv[3].ShouldBe("Total,3,340.00,17.00,357.00,105.00,252.00");

        var (_, october) = (await handler.Handle(new RevenueReportQuery(October1, new DateOnly(2026, 10, 31)), default)).Value;
        october.Totals.Total.ShouldBe(315m);
    }

    [Fact]
    public async Task Parts_usage_report_prices_parts_as_charged_and_costs_them_at_the_current_cost()
    {
        var admin = await SignInOffice(Role.Admin);
        var (customer, site) = await GivenCustomerAndSite();
        var tech = await GivenTechnician();
        var van = await NewDb().StockLocations.Where(l => l.TechnicianId == tech.TechnicianId).Select(l => l.Id).SingleAsync();
        var capacitor = (await Resolve<CreatePartHandler>().Handle(new CreatePartCommand(
            new PartInput("CAP-35", "Run capacitor", null, PartUnit.Pcs, 9m, 18.75m, 0)), default)).Value;
        var gas = (await Resolve<CreatePartHandler>().Handle(new CreatePartCommand(
            new PartInput("R410A", "Refrigerant", null, PartUnit.Kg, 30m, 0m, 0)), default)).Value;
        foreach (var part in new[] { capacitor.Id, gas.Id })
        {
            await Resolve<ReceiveStockHandler>().Handle(new ReceiveStockCommand(new ReceiveInput(part, StockLocation.MainWarehouseId, 20)), default);
            await Resolve<TransferStockHandler>().Handle(new TransferStockCommand(new TransferInput(part, StockLocation.MainWarehouseId, van, 20)), default);
        }

        async Task<Guid> Use(Guid partId, decimal quantity, Guid? workOrderId = null)
        {
            if (workOrderId is null)
            {
                Fixture.CurrentUser.SignInAs(admin.Id, Role.Admin);
                workOrderId = (await GivenWorkOrder(customer, site)).Id;
                await Assign(workOrderId.Value, tech.TechnicianId);
                await Advance(workOrderId.Value, (w, u, now) => w.Dispatch(u, now));
                await Advance(workOrderId.Value, (w, u, now) => w.Start(u, now));
            }
            SignInTechnician(tech);
            (await Resolve<AddWorkOrderPartHandler>().Handle(new AddWorkOrderPartCommand(workOrderId.Value,
                new UsePartInput(partId, quantity)), default)).IsSuccess.ShouldBeTrue();
            return workOrderId.Value;
        }

        var first = await Use(capacitor.Id, 2);
        await Use(gas.Id, 1.5m, first);
        Fixture.CurrentUser.SignInAs(admin.Id, Role.Admin);
        (await Resolve<UpdatePartHandler>().Handle(new UpdatePartCommand(capacitor.Id,
            new PartInput("CAP-35", "Run capacitor", null, PartUnit.Pcs, 10m, 20m, 0)), default)).IsSuccess.ShouldBeTrue();
        await Use(capacitor.Id, 3);
        var returned = await Use(capacitor.Id, 4);
        var line = await NewDb().WorkOrderParts.Where(p => p.WorkOrderId == returned).Select(p => p.Id).SingleAsync();
        (await Resolve<RemoveWorkOrderPartHandler>().Handle(new RemoveWorkOrderPartCommand(returned, line), default)).IsSuccess.ShouldBeTrue();
        Fixture.Clock.Advance(TimeSpan.FromDays(1));
        await Use(capacitor.Id, 1); // on 2 October

        var (period, report) = (await Resolve<PartsUsageReportHandler>().Handle(new PartsUsageReportQuery(October1, October1), default)).Value;

        report.Rows.Select(r => (r.Sku, r.Quantity, r.Jobs, r.Cost, r.Price, r.Margin, r.MarginPercent)).ShouldBe([
            ("CAP-35", 5m, 2, 50m, 97.50m, 47.50m, (decimal?)48.7m), // 2 × 18.75 + 3 × 20, costed at today's 10.00
            ("R410A", 1.5m, 1, 45m, 0m, -45m, (decimal?)null),
        ]);
        (report.Cost, report.Price, report.Margin, report.MarginPercent).ShouldBe((95m, 97.50m, 2.50m, (decimal?)2.6m));
        Text(report.ToCsv(period)).Split("\r\n")[1].ShouldBe("CAP-35,Run capacitor,Pcs,5.00,2,50.00,97.50,47.50,48.70");
    }
}
