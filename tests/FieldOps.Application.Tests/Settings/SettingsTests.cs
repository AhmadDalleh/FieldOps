using FieldOps.Application.Features.Settings;
using FieldOps.Application.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Settings;

public class SettingsTests(PostgresFixture fixture) : TestBase(fixture)
{
    private static UpdateSettingsCommand Valid() =>
        new("Cool Air LLC", "Al Quoz, Dubai", "100123456700003", 5m, 150m, 30, "aed");

    [Fact]
    public async Task Admin_updates_the_company_settings()
    {
        var result = await Resolve<UpdateSettingsHandler>().Handle(Valid(), default);

        result.IsSuccess.ShouldBeTrue();
        var settings = await Resolve<GetSettingsHandler>().Handle(new GetSettingsQuery(), default);
        settings.ShouldBe(new SettingsDto("Cool Air LLC", "Al Quoz, Dubai", "100123456700003", false, 5m, 150m, 30, "AED"));
    }

    [Theory]
    [InlineData(-1, 150, false)]
    [InlineData(101, 150, false)]
    [InlineData(5, -0.01, false)]
    [InlineData(0, 0, true)]
    [InlineData(100, 0, true)]
    public void Vat_rate_must_be_0_to_100_and_labor_rate_at_least_0(decimal vat, decimal labor, bool valid)
    {
        var result = new UpdateSettingsValidator().Validate(Valid() with { VatRate = vat, LaborRatePerHour = labor });

        result.IsValid.ShouldBe(valid);
    }

    [Fact]
    public async Task Invalid_vat_rate_leaves_the_settings_unchanged()
    {
        var result = await Resolve<UpdateSettingsHandler>().Handle(Valid() with { VatRate = 150 }, default);

        result.Error.Code.ShouldBe("Settings.InvalidVatRate");
        (await NewDb().AppSettings.SingleAsync()).VatRate.ShouldBe(5.00m);
    }

    [Fact]
    public async Task Uploading_a_logo_stores_it_and_replaces_the_previous_one()
    {
        var handler = Resolve<UploadLogoHandler>();
        await handler.Handle(new UploadLogoCommand(new MemoryStream([1, 2, 3]), "image/png", 3), default);
        var firstKey = (await NewDb().AppSettings.SingleAsync()).LogoKey;

        var result = await Resolve<UploadLogoHandler>().Handle(new UploadLogoCommand(new MemoryStream([4, 5]), "image/webp", 2), default);

        result.IsSuccess.ShouldBeTrue();
        var secondKey = (await NewDb().AppSettings.SingleAsync()).LogoKey;
        secondKey.ShouldNotBe(firstKey);
        Fixture.Files.Keys.ShouldBe([secondKey!]);
    }

    [Fact]
    public async Task Logo_must_be_an_image()
    {
        var result = await Resolve<UploadLogoHandler>().Handle(
            new UploadLogoCommand(new MemoryStream([1]), "application/pdf", 1), default);

        result.Error.Code.ShouldBe("Settings.InvalidLogoType");
        Fixture.Files.Keys.ShouldBeEmpty();
    }
}
