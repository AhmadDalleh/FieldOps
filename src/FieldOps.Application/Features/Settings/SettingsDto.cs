using FieldOps.Domain.Settings;

namespace FieldOps.Application.Features.Settings;

public sealed record SettingsDto(
    string CompanyName,
    string? CompanyAddress,
    string? Trn,
    bool HasLogo,
    decimal VatRate,
    decimal LaborRatePerHour,
    int InvoiceDueDays,
    string Currency)
{
    public static SettingsDto From(AppSettings s) =>
        new(s.CompanyName, s.CompanyAddress, s.Trn, s.LogoKey is not null, s.VatRate, s.LaborRatePerHour, s.InvoiceDueDays, s.Currency);
}
