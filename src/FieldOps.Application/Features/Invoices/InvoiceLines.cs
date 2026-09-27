using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Invoicing;
using FluentValidation;

namespace FieldOps.Application.Features.Invoices;

public sealed record InvoiceLineInput(InvoiceLineType LineType, string Description, decimal Quantity, decimal UnitPrice);

public sealed class InvoiceLineInputValidator : AbstractValidator<InvoiceLineInput>
{
    public InvoiceLineInputValidator()
    {
        RuleFor(x => x.LineType).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Quantity).GreaterThan(0).PrecisionScale(10, 2, true);
        RuleFor(x => x.UnitPrice).PrecisionScale(18, 2, true);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).When(x => x.LineType != InvoiceLineType.Other)
            .WithMessage("Only Other lines may have a negative price (for a discount).");
    }
}

public sealed record AddInvoiceLineCommand(Guid InvoiceId, InvoiceLineInput Input);

public sealed record UpdateInvoiceLineCommand(Guid InvoiceId, Guid LineId, InvoiceLineInput Input);

public sealed record RemoveInvoiceLineCommand(Guid InvoiceId, Guid LineId);

/// <summary>US-BIL-02: adds a line to a draft (a call-out fee, or a discount as a negative Other line).</summary>
public sealed class AddInvoiceLineHandler(IAppDbContext db, InvoiceReader reader) : ICommandHandler<AddInvoiceLineCommand, Result<InvoiceDto>>
{
    public async Task<Result<InvoiceDto>> Handle(AddInvoiceLineCommand cmd, CancellationToken ct)
    {
        var invoice = await InvoiceStore.LoadAsync(db, cmd.InvoiceId, ct);
        if (invoice.IsFailure) return invoice.Error;
        var input = cmd.Input;
        var line = invoice.Value.AddLine(input.LineType, input.Description, input.Quantity, input.UnitPrice);
        if (line.IsFailure) return line.Error;
        db.InvoiceLines.Add(line.Value);

        var saved = await InvoiceStore.SaveAsync(db, ct);
        return saved.IsFailure ? saved.Error : await reader.ReadAsync(cmd.InvoiceId, ct);
    }
}

public sealed class UpdateInvoiceLineHandler(IAppDbContext db, InvoiceReader reader)
    : ICommandHandler<UpdateInvoiceLineCommand, Result<InvoiceDto>>
{
    public async Task<Result<InvoiceDto>> Handle(UpdateInvoiceLineCommand cmd, CancellationToken ct)
    {
        var invoice = await InvoiceStore.LoadAsync(db, cmd.InvoiceId, ct);
        if (invoice.IsFailure) return invoice.Error;
        var input = cmd.Input;
        var updated = invoice.Value.UpdateLine(cmd.LineId, input.LineType, input.Description, input.Quantity, input.UnitPrice);
        if (updated.IsFailure) return updated.Error;

        var saved = await InvoiceStore.SaveAsync(db, ct);
        return saved.IsFailure ? saved.Error : await reader.ReadAsync(cmd.InvoiceId, ct);
    }
}

public sealed class RemoveInvoiceLineHandler(IAppDbContext db, InvoiceReader reader)
    : ICommandHandler<RemoveInvoiceLineCommand, Result<InvoiceDto>>
{
    public async Task<Result<InvoiceDto>> Handle(RemoveInvoiceLineCommand cmd, CancellationToken ct)
    {
        var invoice = await InvoiceStore.LoadAsync(db, cmd.InvoiceId, ct);
        if (invoice.IsFailure) return invoice.Error;
        var removed = invoice.Value.RemoveLine(cmd.LineId);
        if (removed.IsFailure) return removed.Error;

        var saved = await InvoiceStore.SaveAsync(db, ct);
        return saved.IsFailure ? saved.Error : await reader.ReadAsync(cmd.InvoiceId, ct);
    }
}
