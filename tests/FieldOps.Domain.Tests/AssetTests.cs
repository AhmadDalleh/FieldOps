using FieldOps.Domain.Assets;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class AssetTests
{
    private static AssetDetails Details(DateOnly? installed = null, DateOnly? warranty = null, AssetStatus status = AssetStatus.Active) =>
        new(AssetType.AC, "Split AC - lobby", "Daikin", "FTXM35", "SN-1", installed, warranty, status, null);

    [Theory]
    [InlineData("2026-09-30", "2026-10-01", true)]
    [InlineData("2026-10-01", "2026-10-01", true)]
    [InlineData("2026-10-02", "2026-10-01", false)]
    public void Asset_is_under_warranty_up_to_and_including_the_expiry_date(string today, string expires, bool expected)
    {
        Asset.IsUnderWarranty(DateOnly.Parse(expires), DateOnly.Parse(today)).ShouldBe(expected);
    }

    [Fact]
    public void Asset_without_a_warranty_date_is_not_under_warranty()
    {
        Asset.IsUnderWarranty(null, new DateOnly(2026, 10, 1)).ShouldBeFalse();
    }

    [Fact]
    public void Warranty_cannot_end_before_the_install_date()
    {
        var result = Asset.Create(Guid.CreateVersion7(), Details(new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 30)));

        result.Error.Code.ShouldBe("Asset.WarrantyBeforeInstall");
    }

    [Fact]
    public void Retiring_an_asset_makes_it_inactive()
    {
        var asset = Asset.Create(Guid.CreateVersion7(), Details()).Value;

        asset.Update(Details(status: AssetStatus.Retired));

        asset.IsActive.ShouldBeFalse();
        asset.Status.ShouldBe(AssetStatus.Retired);
    }
}
