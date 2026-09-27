using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Invoicing;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Invoices;

public sealed record MarkPaidInput(DateOnly PaidAt, string Reference);

public sealed class MarkPaidInputValidator : AbstractValidator<MarkPaidInput>
{
    public MarkPaidInputValidator()
    {
        RuleFor(x => x.PaidAt).NotEmpty();
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(100);
    }
}

public sealed record VoidInvoiceInput(string Reason);

public sealed class VoidInvoiceInputValidator : AbstractValidator<VoidInvoiceInput>
{
    public VoidInvoiceInputValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public sealed record IssueInvoiceCommand(Guid Id);

public sealed record MarkInvoicePaidCommand(Guid Id, MarkPaidInput Input);

public sealed record VoidInvoiceCommand(Guid Id, VoidInvoiceInput Input);

public sealed record DeleteDraftInvoiceCommand(Guid Id);

public sealed record GetInvoiceQuery(Guid Id);

public sealed class GetInvoiceHandler(IAppDbContext db, InvoiceReader reader) : IQueryHandler<GetInvoiceQuery, Result<InvoiceDto>>
{
    public async Task<Result<InvoiceDto>> Handle(GetInvoiceQuery query, CancellationToken ct) =>
        await db.Invoices.AnyAsync(i => i.Id == query.Id, ct) ? await reader.ReadAsync(query.Id, ct) : InvoiceErrors.NotFound;
}

/// <summary>
/// US-BIL-03: numbers the draft inside the save's transaction (so a failed issue does not use a number up), invoices the work order,
/// and stores the PDF as issued.
/// </summary>
public sealed class IssueInvoiceHandler(
    IAppDbContext db, INumberSequence numbers, ICurrentUser user, TimeProvider clock, IFileStorage files, InvoicePdfBuilder pdf,
    InvoiceReader reader) : ICommandHandler<IssueInvoiceCommand, Result<InvoiceDto>>
{
    public async Task<Result<InvoiceDto>> Handle(IssueInvoiceCommand cmd, CancellationToken ct)
    {
        var loaded = await InvoiceStore.LoadAsync(db, cmd.Id, ct);
        if (loaded.IsFailure) return loaded.Error;
        var invoice = loaded.Value;
        if (!invoice.IsDraft) return InvoiceErrors.NotDraft;
        var workOrder = await db.WorkOrders.SingleAsync(w => w.Id == invoice.WorkOrderId, ct);
        var dueDays = await db.AppSettings.AsNoTracking().Select(s => s.InvoiceDueDays).SingleAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var number = await numbers.NextAsync(Invoice.NumberSequence, ct);
        var issued = invoice.Issue(number, clock.Today(), dueDays, workOrder, user.UserId, clock.GetUtcNow());
        if (issued.IsFailure) return issued.Error;

        var key = $"invoices/{invoice.Id}.pdf";
        await files.SaveAsync(new MemoryStream(await pdf.RenderAsync(invoice, ct)), key, ct);
        invoice.AttachPdf(key);
        var saved = await InvoiceStore.SaveAsync(db, ct);
        if (saved.IsFailure)
        {
            await files.DeleteAsync(key, ct);
            return saved.Error;
        }
        await tx.CommitAsync(ct);
        return await reader.ReadAsync(invoice.Id, ct);
    }
}

/// <summary>US-BIL-05.</summary>
public sealed class MarkInvoicePaidHandler(IAppDbContext db, TimeProvider clock, InvoiceReader reader)
    : ICommandHandler<MarkInvoicePaidCommand, Result<InvoiceDto>>
{
    public async Task<Result<InvoiceDto>> Handle(MarkInvoicePaidCommand cmd, CancellationToken ct)
    {
        var invoice = await InvoiceStore.LoadAsync(db, cmd.Id, ct);
        if (invoice.IsFailure) return invoice.Error;
        var paid = invoice.Value.MarkPaid(cmd.Input.PaidAt, cmd.Input.Reference, clock.Today());
        if (paid.IsFailure) return paid.Error;

        var saved = await InvoiceStore.SaveAsync(db, ct);
        return saved.IsFailure ? saved.Error : await reader.ReadAsync(cmd.Id, ct);
    }
}

/// <summary>US-BIL-06: voids an issued invoice and puts the work order back to Completed for a new invoice.</summary>
public sealed class VoidInvoiceHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, InvoiceReader reader)
    : ICommandHandler<VoidInvoiceCommand, Result<InvoiceDto>>
{
    public async Task<Result<InvoiceDto>> Handle(VoidInvoiceCommand cmd, CancellationToken ct)
    {
        var loaded = await InvoiceStore.LoadAsync(db, cmd.Id, ct);
        if (loaded.IsFailure) return loaded.Error;
        var invoice = loaded.Value;
        var workOrder = await db.WorkOrders.SingleAsync(w => w.Id == invoice.WorkOrderId, ct);
        var voided = invoice.Void(cmd.Input.Reason, workOrder, user.UserId, clock.GetUtcNow());
        if (voided.IsFailure) return voided.Error;

        var saved = await InvoiceStore.SaveAsync(db, ct);
        return saved.IsFailure ? saved.Error : await reader.ReadAsync(cmd.Id, ct);
    }
}

/// <summary>Drafts can be thrown away (docs/07-flows.md §2); anything issued stays for the audit trail.</summary>
public sealed class DeleteDraftInvoiceHandler(IAppDbContext db) : ICommandHandler<DeleteDraftInvoiceCommand, Result>
{
    public async Task<Result> Handle(DeleteDraftInvoiceCommand cmd, CancellationToken ct)
    {
        var invoice = await InvoiceStore.LoadAsync(db, cmd.Id, ct);
        if (invoice.IsFailure) return invoice.Error;
        if (!invoice.Value.IsDraft) return InvoiceErrors.NotDraft;

        db.Invoices.Remove(invoice.Value);
        return await InvoiceStore.SaveAsync(db, ct);
    }
}
