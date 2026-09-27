using FieldOps.Domain.Customers;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class SiteTests
{
    private static SiteDetails Details(double? lat, double? lng) =>
        new("HQ", "Sheikh Zayed Road", null, "Dubai", "Dubai", null, lat, lng, null);

    [Theory]
    [InlineData(-90.0, -180.0, true)]
    [InlineData(90.0, 180.0, true)]
    [InlineData(25.2048, 55.2708, true)]
    [InlineData(-90.0001, 0.0, false)]
    [InlineData(90.0001, 0.0, false)]
    [InlineData(0.0, -180.0001, false)]
    [InlineData(0.0, 180.0001, false)]
    public void Coordinates_must_be_within_range(double lat, double lng, bool valid)
    {
        var result = Site.Create(Guid.CreateVersion7(), Details(lat, lng));

        result.IsSuccess.ShouldBe(valid);
    }

    [Fact]
    public void Coordinates_are_optional_and_country_defaults_to_AE()
    {
        var site = Site.Create(Guid.CreateVersion7(), Details(null, null)).Value;

        site.Latitude.ShouldBeNull();
        site.Country.ShouldBe("AE");
    }

    [Fact]
    public void Latitude_without_longitude_is_rejected()
    {
        Site.Create(Guid.CreateVersion7(), Details(25, null)).Error.Code.ShouldBe("Site.IncompleteLocation");
    }
}
