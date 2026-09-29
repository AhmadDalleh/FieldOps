using System.Text;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Inventory;
using FieldOps.Application.Features.Invoices;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Inventory;
using FieldOps.Domain.Invoicing;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Invoices;

public class InvoiceTests(PostgresFixture fixture) : TestBase(fixture)
{
    private Guid _customer;
    private Guid _site;

    /// <summary>
    /// A completed job with 70 minutes of work (billed as 1.25 h at 120.00) and two capacitors at 18.75, so the draft is
    /// 150.00 + 37.50 = 187.50 subtotal, 9.38 VAT and 196.88 total (docs/08-testing.md).
    /// </summary>
    private async Task<Guid> CompletedJob()
    {
        var admin = await SignInOffice(Role.Admin);
        await SetLaborRate(120m);
        (_customer, _site) = await GivenCustomerAndSite();
        var tech = await GivenTechnician();
        var van = await NewDb().StockLocations.Where(l => l.TechnicianId == tech.TechnicianId).Select(l => l.Id).SingleAsync();
        var part = (await Resolve<CreatePartHandler>().Handle(new CreatePartCommand(
            new PartInput("CAP-35", "Run capacitor", null, PartUnit.Pcs, 9m, 18.75m, 0)), default)).Value;
        await Resolve<ReceiveStockHandler>().Handle(new ReceiveStockCommand(new ReceiveInput(part.Id, StockLocation.MainWarehouseId, 5)), default);
        await Resolve<TransferStockHandler>().Handle(new TransferStockCommand(new TransferInput(part.Id, StockLocation.MainWarehouseId, van, 5)), default);

        var wo = await GivenWorkOrder(_customer, _site);
        await Assign(wo.Id, tech.TechnicianId);
        await Advance(wo.Id, (w, u, now) => w.Dispatch(u, now));
        await Advance(wo.Id, (w, u, now) => w.Start(u, now));
        SignInTechnician(tech);
        (await Resolve<AddWorkOrderPartHandler>().Handle(new AddWorkOrderPartCommand(wo.Id, new UsePartInput(part.Id, 2)), default))
            .IsSuccess.ShouldBeTrue();
        Fixture.Clock.Advance(TimeSpan.FromMinutes(70));
        var signature = await GivenSignature(wo.Id);
        await Advance(wo.Id, (w, u, now) => w.Complete("Replaced capacitor", "Sara M.", signature, u, now, "Not needed"));

        Fixture.CurrentUser.SignInAs(admin.Id, Role.Admin);
        return wo.Id;
    }

    private async Task SetLaborRate(decimal rate)
    {
        var db = NewDb();
        var settings = await db.AppSettings.SingleAsync();
        settings.Update("FieldOps Services", "Dubai", "100000000000003", 5m, rate, 30, "AED").IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync();
    }

    private Task<Result<InvoiceDto>> Generate(Guid workOrderId) =>
        Resolve<GenerateInvoiceHandler>().Handle(new GenerateInvoiceCommand(workOrderId), default);

    private Task<Result<InvoiceDto>> Issue(Guid invoiceId) =>
        Resolve<IssueInvoiceHandler>().Handle(new IssueInvoiceCommand(invoiceId), default);

    private async Task<WorkOrderStatus> StatusOf(Guid workOrderId) =>
        await NewDb().WorkOrders.Where(w => w.Id == workOrderId).Select(w => w.Status).SingleAsync();

    [Fact]
    public async Task Generate_bills_rounded_work_time_and_the_parts_used()
    {
        var job = await CompletedJob();

        var invoice = (await Generate(job)).Value;

        invoice.Status.ShouldBe(InvoiceStatus.Draft);
        invoice.Number.ShouldBeNull();
        invoice.CustomerId.ShouldBe(_customer);
        invoice.Lines.Select(l => (l.LineType, l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ShouldBe([
            (InvoiceLineType.Labor, "Labor (1.25 h)", 1.25m, 120m, 150m),
            (InvoiceLineType.Part, "Run capacitor (CAP-35)", 2m, 18.75m, 37.50m),
        ]);
        invoice.Subtotal.ShouldBe(187.50m);
        invoice.VatRate.ShouldBe(5m);
        invoice.VatAmount.ShouldBe(9.38m);
        invoice.Total.ShouldBe(196.88m);
    }

    [Fact]
    public async Task Generate_refuses_a_job_that_is_not_completed()
    {
        await SignInOffice();
        (_customer, _site) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(_customer, _site);

        (await Generate(wo.Id)).Error.ShouldBe(InvoiceErrors.WorkOrderNotCompleted);
    }

    [Fact]
    public async Task A_work_order_has_one_live_invoice_even_when_two_requests_race()
    {
        var job = await CompletedJob();

        var results = await Task.WhenAll(Generate(job), Generate(job));

        results.Count(r => r.IsSuccess).ShouldBe(1);
        results.Single(r => r.IsFailure).Error.ShouldBe(InvoiceErrors.AlreadyInvoiced);
        (await Generate(job)).Error.ShouldBe(InvoiceErrors.AlreadyInvoiced);
    }

    [Fact]
    public async Task Draft_lines_can_be_added_changed_and_removed_with_totals_recalculated()
    {
        var invoice = (await Generate(await CompletedJob())).Value;

        var added = (await Resolve<AddInvoiceLineHandler>().Handle(new AddInvoiceLineCommand(invoice.Id,
            new InvoiceLineInput(InvoiceLineType.Other, "Call-out fee", 1, 50m)), default)).Value;
        added.Total.ShouldBe(249.38m); // 237.50 + 11.88
        var fee = added.Lines.Last();

        var discounted = (await Resolve<UpdateInvoiceLineHandler>().Handle(new UpdateInvoiceLineCommand(invoice.Id, fee.Id,
            new InvoiceLineInput(InvoiceLineType.Other, "Loyalty discount", 1, -37.50m)), default)).Value;
        discounted.Subtotal.ShouldBe(150m);
        discounted.Total.ShouldBe(157.50m);

        var removed = (await Resolve<RemoveInvoiceLineHandler>().Handle(new RemoveInvoiceLineCommand(invoice.Id, fee.Id), default)).Value;
        removed.Lines.Count.ShouldBe(2);
        removed.Total.ShouldBe(196.88m);

        (await Resolve<AddInvoiceLineHandler>().Handle(new AddInvoiceLineCommand(invoice.Id,
            new InvoiceLineInput(InvoiceLineType.Other, "Too much", 1, -500m)), default)).Error.ShouldBe(InvoiceErrors.NegativeTotal);
        (await Resolve<GetInvoiceHandler>().Handle(new GetInvoiceQuery(invoice.Id), default)).Value.Total.ShouldBe(196.88m);
    }

    [Fact]
    public async Task Issue_numbers_the_invoice_invoices_the_job_and_stores_the_pdf()
    {
        var job = await CompletedJob();
        var draft = (await Generate(job)).Value;
        Fixture.Clock.Advance(TimeSpan.FromDays(1));

        var issued = (await Issue(draft.Id)).Value;

        issued.Status.ShouldBe(InvoiceStatus.Issued);
        issued.Number.ShouldBe("INV-000001");
        issued.IssueDate.ShouldBe(new DateOnly(2026, 10, 2));
        issued.DueDate.ShouldBe(new DateOnly(2026, 11, 1));
        (await StatusOf(job)).ShouldBe(WorkOrderStatus.Invoiced);

        var pdf = (await Resolve<GetInvoicePdfHandler>().Handle(new GetInvoicePdfQuery(draft.Id), default)).Value;
        pdf.FileName.ShouldBe("INV-000001.pdf");
        Fixture.Files.Keys.ShouldContain($"invoices/{draft.Id}.pdf");
        using var reader = new StreamReader(pdf.Content, Encoding.ASCII);
        (await reader.ReadToEndAsync()).ShouldStartWith("%PDF");
    }

    [Fact]
    public async Task An_issued_invoice_is_read_only_and_cannot_be_deleted()
    {
        var draft = (await Generate(await CompletedJob())).Value;
        await Issue(draft.Id);

        (await Resolve<AddInvoiceLineHandler>().Handle(new AddInvoiceLineCommand(draft.Id,
            new InvoiceLineInput(InvoiceLineType.Other, "Fee", 1, 5m)), default)).Error.ShouldBe(InvoiceErrors.NotDraft);
        (await Resolve<RemoveInvoiceLineHandler>().Handle(new RemoveInvoiceLineCommand(draft.Id, draft.Lines[0].Id), default))
            .Error.ShouldBe(InvoiceErrors.NotDraft);
        (await Issue(draft.Id)).Error.ShouldBe(InvoiceErrors.NotDraft);
        (await Resolve<DeleteDraftInvoiceHandler>().Handle(new DeleteDraftInvoiceCommand(draft.Id), default))
            .Error.ShouldBe(InvoiceErrors.NotDraft);
    }

    [Fact]
    public async Task Numbers_are_sequential_and_a_failed_issue_does_not_use_one_up()
    {
        var first = (await Generate(await CompletedJob())).Value;
        var empty = (await Generate(await CompletedJobWithoutWork())).Value;

        (await Issue(empty.Id)).Error.ShouldBe(InvoiceErrors.Empty);
        (await Issue(first.Id)).Value.Number.ShouldBe("INV-000001");
    }

    private async Task<Guid> CompletedJobWithoutWork()
    {
        var wo = await GivenWorkOrder(_customer, _site, title: "Quick look");
        await Complete(wo.Id, (await GivenTechnician()).TechnicianId);
        return wo.Id;
    }

    [Fact]
    public async Task Deleting_a_draft_frees_the_job_for_a_new_one()
    {
        var job = await CompletedJob();
        var draft = (await Generate(job)).Value;

        (await Resolve<DeleteDraftInvoiceHandler>().Handle(new DeleteDraftInvoiceCommand(draft.Id), default)).IsSuccess.ShouldBeTrue();

        (await NewDb().InvoiceLines.AnyAsync(l => l.InvoiceId == draft.Id)).ShouldBeFalse();
        (await Generate(job)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Mark_paid_records_the_payment()
    {
        var draft = (await Generate(await CompletedJob())).Value;
        await Issue(draft.Id);

        var paid = (await Resolve<MarkInvoicePaidHandler>().Handle(new MarkInvoicePaidCommand(draft.Id,
            new MarkPaidInput(new DateOnly(2026, 10, 1), "TT-2291")), default)).Value;

        paid.Status.ShouldBe(InvoiceStatus.Paid);
        paid.PaidAt.ShouldBe(new DateOnly(2026, 10, 1));
        paid.PaymentReference.ShouldBe("TT-2291");
    }

    [Fact]
    public async Task Void_returns_the_job_to_completed_so_it_can_be_invoiced_again()
    {
        var job = await CompletedJob();
        var draft = (await Generate(job)).Value;
        await Issue(draft.Id);

        var voided = (await Resolve<VoidInvoiceHandler>().Handle(new VoidInvoiceCommand(draft.Id, new VoidInvoiceInput("Wrong rate")),
            default)).Value;

        voided.Status.ShouldBe(InvoiceStatus.Void);
        voided.VoidReason.ShouldBe("Wrong rate");
        (await StatusOf(job)).ShouldBe(WorkOrderStatus.Completed);
        var again = (await Generate(job)).Value;
        (await Issue(again.Id)).Value.Number.ShouldBe("INV-000002");
        var detail = await Resolve<WorkOrderReader>().ReadAsync(job, default);
        detail.Invoice.ShouldBe(new WorkOrderInvoice(again.Id, "INV-000002", InvoiceStatus.Issued));
    }

    [Fact]
    public async Task A_paid_invoice_cannot_be_voided()
    {
        var job = await CompletedJob();
        var draft = (await Generate(job)).Value;
        await Issue(draft.Id);
        await Resolve<MarkInvoicePaidHandler>().Handle(new MarkInvoicePaidCommand(draft.Id,
            new MarkPaidInput(new DateOnly(2026, 10, 1), "TT-1")), default);

        (await Resolve<VoidInvoiceHandler>().Handle(new VoidInvoiceCommand(draft.Id, new VoidInvoiceInput("Oops")), default))
            .Error.ShouldBe(InvoiceErrors.PaidCannotBeVoided);
        (await StatusOf(job)).ShouldBe(WorkOrderStatus.Invoiced);
    }

    [Fact]
    public async Task The_list_filters_by_status_customer_date_and_overdue()
    {
        var job = await CompletedJob();
        var issued = (await Generate(job)).Value;
        await Issue(issued.Id);
        Fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        var draft = (await Generate(await CompletedJobWithoutWork())).Value;
        var list = Resolve<ListInvoicesHandler>();
        var page = new PageRequest();

        (await list.Handle(new ListInvoicesQuery(page), default)).Items.Select(i => i.Id).ShouldBe([draft.Id, issued.Id]);
        (await list.Handle(new ListInvoicesQuery(page, Status: InvoiceStatus.Draft), default)).Items.ShouldHaveSingleItem().Id.ShouldBe(draft.Id);
        (await list.Handle(new ListInvoicesQuery(page, CustomerId: Guid.NewGuid()), default)).Items.ShouldBeEmpty();
        (await list.Handle(new ListInvoicesQuery(page with { Search = "inv-000001" }), default)).Items.ShouldHaveSingleItem();
        (await list.Handle(new ListInvoicesQuery(page, From: new DateOnly(2026, 10, 2)), default)).Items.ShouldBeEmpty();
        (await list.Handle(new ListInvoicesQuery(page, From: new DateOnly(2026, 10, 1), To: new DateOnly(2026, 10, 1)), default))
            .TotalCount.ShouldBe(2);
        (await list.Handle(new ListInvoicesQuery(page, OverdueOnly: true), default)).Items.ShouldBeEmpty();

        Fixture.Clock.Advance(TimeSpan.FromDays(31));
        var overdue = (await Resolve<ListInvoicesHandler>().Handle(new ListInvoicesQuery(page, OverdueOnly: true), default)).Items;
        overdue.ShouldHaveSingleItem().ShouldSatisfyAllConditions(i => i.Id.ShouldBe(issued.Id), i => i.IsOverdue.ShouldBeTrue());
    }
}
