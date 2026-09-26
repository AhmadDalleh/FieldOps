using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Settings;

public sealed record UpdateSettingsCommand(
    string CompanyName,
    string? CompanyAddress,
    string? Trn,
    decimal VatRate,
    decimal LaborRatePerHour,
    int InvoiceDueDays,
    string Currency);

public sealed class UpdateSettingsValidator : AbstractValidator<UpdateSettingsCommand>
{
    public UpdateSettingsValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CompanyAddress).MaximumLength(500);
        RuleFor(x => x.Trn).MaximumLength(50);
        RuleFor(x => x.VatRate).InclusiveBetween(0, 100);
        RuleFor(x => x.LaborRatePerHour).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InvoiceDueDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

public sealed class UpdateSettingsHandler(IAppDbContext db) : ICommandHandler<UpdateSettingsCommand, Result<SettingsDto>>
{
    public async Task<Result<SettingsDto>> Handle(UpdateSettingsCommand cmd, CancellationToken ct)
    {
        var settings = await db.AppSettings.SingleAsync(ct);
        var result = settings.Update(
            cmd.CompanyName.Trim(),
            cmd.CompanyAddress,
            cmd.Trn,
            cmd.VatRate,
            cmd.LaborRatePerHour,
            cmd.InvoiceDueDays,
            cmd.Currency.Trim().ToUpperInvariant());
        if (result.IsFailure) return result.Error;

        await db.SaveChangesAsync(ct);
        return SettingsDto.From(settings);
    }
}
