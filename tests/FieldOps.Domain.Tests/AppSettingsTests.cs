using FieldOps.Domain.Settings;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class AppSettingsTests
{
    [Theory]
    [InlineData(-0.01, false)]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(100, true)]
    [InlineData(100.01, false)]
    public void Vat_rate_must_be_between_0_and_100(decimal vatRate, bool allowed)
    {
        var settings = AppSettings.CreateDefault();

        var result = settings.Update("Acme", null, null, vatRate, 150m, 30, "AED");

        result.IsSuccess.ShouldBe(allowed);
        settings.VatRate.ShouldBe(allowed ? vatRate : 5.00m);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(150, true)]
    public void Labor_rate_must_be_at_least_0(decimal laborRate, bool allowed)
    {
        var settings = AppSettings.CreateDefault();

        var result = settings.Update("Acme", null, null, 5m, laborRate, 30, "AED");

        result.IsSuccess.ShouldBe(allowed);
        if (!allowed) result.Error.Code.ShouldBe("Settings.InvalidLaborRate");
    }

    [Fact]
    public void Defaults_are_5_percent_vat_30_due_days_and_aed()
    {
        var settings = AppSettings.CreateDefault();

        settings.VatRate.ShouldBe(5.00m);
        settings.InvoiceDueDays.ShouldBe(30);
        settings.Currency.ShouldBe("AED");
    }
}
