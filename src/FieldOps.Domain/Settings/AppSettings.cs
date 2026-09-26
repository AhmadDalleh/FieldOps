using FieldOps.Domain.Common;

namespace FieldOps.Domain.Settings;

public sealed class AppSettings : AuditableEntity
{
    public static readonly Guid SingletonId = new("01925b6e-0000-7000-8000-000000000001");

    private AppSettings() { }

    public string CompanyName { get; private set; } = "FieldOps";
    public string? CompanyAddress { get; private set; }
    public string? Trn { get; private set; }
    public string? LogoKey { get; private set; }
    public decimal VatRate { get; private set; } = 5.00m;
    public decimal LaborRatePerHour { get; private set; }
    public int InvoiceDueDays { get; private set; } = 30;
    public string Currency { get; private set; } = "AED";

    public static AppSettings CreateDefault() => new() { Id = SingletonId };

    public Result Update(
        string companyName,
        string? companyAddress,
        string? trn,
        decimal vatRate,
        decimal laborRatePerHour,
        int invoiceDueDays,
        string currency)
    {
        if (vatRate is < 0 or > 100)
            return Error.Validation("Settings.InvalidVatRate", "The VAT rate must be between 0 and 100.");
        if (laborRatePerHour < 0)
            return Error.Validation("Settings.InvalidLaborRate", "The labor rate must be at least 0.");
        if (invoiceDueDays < 0)
            return Error.Validation("Settings.InvalidInvoiceDueDays", "Invoice due days must be at least 0.");

        CompanyName = companyName;
        CompanyAddress = companyAddress;
        Trn = trn;
        VatRate = vatRate;
        LaborRatePerHour = laborRatePerHour;
        InvoiceDueDays = invoiceDueDays;
        Currency = currency;
        return Result.Success();
    }

    public void SetLogo(string logoKey) => LogoKey = logoKey;
}
