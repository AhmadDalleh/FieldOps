using FieldOps.Domain.Invoicing;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class InvoiceCalculatorTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0.25)]
    [InlineData(15, 0.25)]
    [InlineData(16, 0.5)]
    [InlineData(60, 1)]
    [InlineData(70, 1.25)]
    [InlineData(91, 1.75)]
    public void Labor_hours_round_up_to_the_next_quarter_hour(int minutes, double hours) =>
        InvoiceCalculator.LaborHours(minutes).ShouldBe((decimal)hours);

    [Fact]
    public void Vat_on_187_50_at_5_percent_is_9_38() =>
        InvoiceCalculator.Vat(187.50m, 5m).ShouldBe(9.38m);

    [Theory]
    [InlineData(0.125, 0.13)]
    [InlineData(-0.125, -0.13)]
    [InlineData(2.674, 2.67)]
    public void Money_rounds_half_away_from_zero(decimal amount, decimal rounded) =>
        InvoiceCalculator.Round(amount).ShouldBe(rounded);

    [Fact]
    public void Totals_add_vat_to_the_subtotal()
    {
        var (subtotal, vat, total) = InvoiceCalculator.Totals([150m, 37.50m], 5m);
        subtotal.ShouldBe(187.50m);
        vat.ShouldBe(9.38m);
        total.ShouldBe(196.88m);
    }

    [Fact]
    public void Line_totals_are_rounded_to_fils() =>
        InvoiceCalculator.LineTotal(1.25m, 33.33m).ShouldBe(41.66m);
}

public class InvoiceTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 1);

    private static WorkOrder Completed()
    {
        var wo = WorkOrder.Create("WO-000001", Guid.NewGuid(), Guid.NewGuid(),
            new WorkOrderDetails("AC not cooling", null, WorkOrderType.Repair, WorkOrderPriority.Medium, null, null), [], User, Now);
        wo.Schedule(Guid.NewGuid(), Now.AddHours(1), Now.AddHours(2), User, Now).IsSuccess.ShouldBeTrue();
        wo.Dispatch(User, Now).IsSuccess.ShouldBeTrue();
        wo.Start(User, Now).IsSuccess.ShouldBeTrue();
        wo.Complete("Done", "Sara M.", Guid.NewGuid(), User, Now).IsSuccess.ShouldBeTrue();
        return wo;
    }

    private static Invoice Draft(WorkOrder wo, int minutes = 70, params BillablePart[] parts) =>
        Invoice.Generate(wo, minutes, 120m, 5m, parts).Value;

    [Fact]
    public void Generate_bills_rounded_labor_and_each_part_at_its_snapshot_price()
    {
        var wo = Completed();
        var invoice = Draft(wo, 70, new BillablePart("FLT-01 Filter", 2, 18.75m));

        invoice.Status.ShouldBe(InvoiceStatus.Draft);
        invoice.Number.ShouldBeNull();
        invoice.CustomerId.ShouldBe(wo.CustomerId);
        invoice.Lines.Count.ShouldBe(2);
        invoice.Lines[0].ShouldSatisfyAllConditions(
            l => l.LineType.ShouldBe(InvoiceLineType.Labor),
            l => l.Quantity.ShouldBe(1.25m),
            l => l.LineTotal.ShouldBe(150m));
        invoice.Lines[1].LineTotal.ShouldBe(37.50m);
        invoice.Subtotal.ShouldBe(187.50m);
        invoice.VatAmount.ShouldBe(9.38m);
        invoice.Total.ShouldBe(196.88m);
    }

    [Fact]
    public void Generate_leaves_out_labor_when_no_work_time_was_logged() =>
        Draft(Completed(), 0).Lines.ShouldBeEmpty();

    [Fact]
    public void Generate_fails_unless_the_work_order_is_completed()
    {
        var wo = WorkOrder.Create("WO-000002", Guid.NewGuid(), Guid.NewGuid(),
            new WorkOrderDetails("Check", null, WorkOrderType.Inspection, WorkOrderPriority.Low, null, null), [], User, Now);
        Invoice.Generate(wo, 60, 100m, 5m, []).Error.ShouldBe(InvoiceErrors.WorkOrderNotCompleted);
    }

    [Fact]
    public void Editing_lines_recalculates_the_totals()
    {
        var invoice = Draft(Completed());
        var fee = invoice.AddLine(InvoiceLineType.Other, "Call-out fee", 1, 50m).Value;
        invoice.Total.ShouldBe(210m); // (150 + 50) × 1.05

        invoice.UpdateLine(fee.Id, InvoiceLineType.Other, "Discount", 1, -20m).IsSuccess.ShouldBeTrue();
        invoice.Subtotal.ShouldBe(130m);
        invoice.Total.ShouldBe(136.50m);

        invoice.RemoveLine(fee.Id).IsSuccess.ShouldBeTrue();
        invoice.Total.ShouldBe(157.50m);
    }

    [Fact]
    public void The_total_cannot_go_negative()
    {
        var invoice = Draft(Completed());
        invoice.AddLine(InvoiceLineType.Other, "Goodwill", 1, -150.01m).Error.ShouldBe(InvoiceErrors.NegativeTotal);
        var discount = invoice.AddLine(InvoiceLineType.Other, "Goodwill", 1, -150m).Value;
        invoice.Total.ShouldBe(0m);

        invoice.RemoveLine(invoice.Lines[0].Id).Error.ShouldBe(InvoiceErrors.NegativeTotal);
        invoice.UpdateLine(discount.Id, InvoiceLineType.Other, "Goodwill", 2, -150m).Error.ShouldBe(InvoiceErrors.NegativeTotal);
        invoice.Lines.Count.ShouldBe(2);
        invoice.Total.ShouldBe(0m);
    }

    [Theory]
    [InlineData(InvoiceLineType.Part, "Filter", 1, -5)]
    [InlineData(InvoiceLineType.Other, "", 1, 5)]
    [InlineData(InvoiceLineType.Other, "Fee", 0, 5)]
    [InlineData(InvoiceLineType.Other, "Fee", 1.005, 5)]
    [InlineData(InvoiceLineType.Labor, "Labor", 1, 10.001)]
    public void Invalid_lines_are_rejected(InvoiceLineType type, string description, double quantity, double price) =>
        Draft(Completed()).AddLine(type, description, (decimal)quantity, (decimal)price).Error.ShouldBe(InvoiceErrors.InvalidLine);

    [Fact]
    public void Issue_numbers_the_invoice_sets_its_dates_and_invoices_the_work_order()
    {
        var wo = Completed();
        var invoice = Draft(wo);

        invoice.Issue(45, Today, 30, wo, User, Now).IsSuccess.ShouldBeTrue();

        invoice.Number.ShouldBe("INV-000045");
        invoice.Status.ShouldBe(InvoiceStatus.Issued);
        invoice.IssueDate.ShouldBe(Today);
        invoice.DueDate.ShouldBe(new DateOnly(2026, 10, 31));
        wo.Status.ShouldBe(WorkOrderStatus.Invoiced);
    }

    [Fact]
    public void An_issued_invoice_is_read_only()
    {
        var wo = Completed();
        var invoice = Draft(wo);
        invoice.Issue(1, Today, 30, wo, User, Now);

        invoice.AddLine(InvoiceLineType.Other, "Fee", 1, 5m).Error.ShouldBe(InvoiceErrors.NotDraft);
        invoice.UpdateLine(invoice.Lines[0].Id, InvoiceLineType.Labor, "Labor", 1, 1m).Error.ShouldBe(InvoiceErrors.NotDraft);
        invoice.RemoveLine(invoice.Lines[0].Id).Error.ShouldBe(InvoiceErrors.NotDraft);
        invoice.Issue(2, Today, 30, wo, User, Now).Error.ShouldBe(InvoiceErrors.NotDraft);
    }

    [Fact]
    public void An_empty_draft_cannot_be_issued()
    {
        var wo = Completed();
        Draft(wo, 0).Issue(1, Today, 30, wo, User, Now).Error.ShouldBe(InvoiceErrors.Empty);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void Mark_paid_records_the_date_and_reference()
    {
        var wo = Completed();
        var invoice = Draft(wo);
        invoice.MarkPaid(Today, "TT-1", Today).Error.ShouldBe(InvoiceErrors.NotIssued);
        invoice.Issue(1, Today, 30, wo, User, Now);

        invoice.MarkPaid(Today.AddDays(-1), "TT-1", Today).Error.ShouldBe(InvoiceErrors.InvalidPaidDate);
        invoice.MarkPaid(Today.AddDays(1), "TT-1", Today).Error.ShouldBe(InvoiceErrors.InvalidPaidDate);
        invoice.MarkPaid(Today, " ", Today).Error.ShouldBe(InvoiceErrors.ReferenceRequired);
        invoice.MarkPaid(Today, " TT-1 ", Today).IsSuccess.ShouldBeTrue();

        invoice.Status.ShouldBe(InvoiceStatus.Paid);
        invoice.PaidAt.ShouldBe(Today);
        invoice.PaymentReference.ShouldBe("TT-1");
    }

    [Fact]
    public void Void_needs_a_reason_and_returns_the_work_order_to_completed()
    {
        var wo = Completed();
        var invoice = Draft(wo);
        invoice.Void("Wrong rate", wo, User, Now).Error.ShouldBe(InvoiceErrors.NotIssued);
        invoice.Issue(1, Today, 30, wo, User, Now);

        invoice.Void("  ", wo, User, Now).Error.ShouldBe(InvoiceErrors.ReasonRequired);
        invoice.Void("Wrong rate", wo, User, Now).IsSuccess.ShouldBeTrue();

        invoice.Status.ShouldBe(InvoiceStatus.Void);
        invoice.VoidReason.ShouldBe("Wrong rate");
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
        Invoice.Generate(wo, 70, 120m, 5m, []).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void A_paid_invoice_cannot_be_voided()
    {
        var wo = Completed();
        var invoice = Draft(wo);
        invoice.Issue(1, Today, 30, wo, User, Now);
        invoice.MarkPaid(Today, "TT-1", Today);

        invoice.Void("Oops", wo, User, Now).Error.ShouldBe(InvoiceErrors.PaidCannotBeVoided);
        wo.Status.ShouldBe(WorkOrderStatus.Invoiced);
    }

    [Fact]
    public void Overdue_means_issued_and_past_the_due_date()
    {
        var wo = Completed();
        var invoice = Draft(wo);
        invoice.IsOverdue(Today.AddDays(60)).ShouldBeFalse();
        invoice.Issue(1, Today, 30, wo, User, Now);

        invoice.IsOverdue(Today.AddDays(30)).ShouldBeFalse();
        invoice.IsOverdue(Today.AddDays(31)).ShouldBeTrue();
        invoice.MarkPaid(Today, "TT-1", Today);
        invoice.IsOverdue(Today.AddDays(31)).ShouldBeFalse();
    }
}
