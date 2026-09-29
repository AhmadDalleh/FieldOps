using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Sites;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Customers;
using Shouldly;

namespace FieldOps.Application.Tests.Customers;

public class SiteTests(PostgresFixture fixture) : TestBase(fixture)
{
    private static SiteInput Input(double? lat = 25.2048, double? lng = 55.2708) =>
        new("Head office", "Sheikh Zayed Road", "Tower 2", "Dubai", "Dubai", null, lat, lng, "Gate code 1234, park in B2");

    private async Task<Guid> GivenCustomer() =>
        (await Resolve<CreateCustomerHandler>().Handle(new CreateCustomerCommand(
            new CustomerInput("Acme", CustomerType.Business, null, "+971500000000", null, null, null), Force: true), default)).Value.Id;

    private async Task<SiteDto> GivenSite(Guid customerId) =>
        (await Resolve<AddSiteHandler>().Handle(new AddSiteCommand(customerId, Input()), default)).Value;

    [Fact]
    public async Task Site_is_added_with_address_pin_and_access_notes()
    {
        var customerId = await GivenCustomer();

        var site = await GivenSite(customerId);

        site.ShouldBe(new SiteDto(site.Id, customerId, "Head office", "Sheikh Zayed Road", "Tower 2", "Dubai", "Dubai", "AE",
            25.2048, 55.2708, "Gate code 1234, park in B2", true));
    }

    [Theory]
    [InlineData("", "Road", "Dubai", false)]
    [InlineData("HQ", "", "Dubai", false)]
    [InlineData("HQ", "Road", "", false)]
    [InlineData("HQ", "Road", "Dubai", true)]
    public void Name_address_line_1_and_city_are_required(string name, string line1, string city, bool valid)
    {
        new SiteInputValidator().Validate(Input() with { Name = name, AddressLine1 = line1, City = city }).IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData(91.0, 55.0, false)]
    [InlineData(-91.0, 55.0, false)]
    [InlineData(25.0, 181.0, false)]
    [InlineData(25.0, -181.0, false)]
    [InlineData(null, null, true)]
    public void Coordinates_must_be_in_range_when_given(double? lat, double? lng, bool valid)
    {
        new SiteInputValidator().Validate(Input(lat, lng)).IsValid.ShouldBe(valid);
    }

    [Fact]
    public async Task Out_of_range_coordinates_are_rejected_by_the_handler_too()
    {
        var customerId = await GivenCustomer();

        var result = await Resolve<AddSiteHandler>().Handle(new AddSiteCommand(customerId, Input(95, 55)), default);

        result.Error.Code.ShouldBe("Site.InvalidLatitude");
    }

    [Fact]
    public async Task Access_notes_are_optional()
    {
        var customerId = await GivenCustomer();

        var result = await Resolve<AddSiteHandler>().Handle(new AddSiteCommand(customerId, Input() with { AccessNotes = null }), default);

        result.Value.AccessNotes.ShouldBeNull();
    }

    [Fact]
    public async Task Site_can_be_edited()
    {
        var site = await GivenSite(await GivenCustomer());

        await Resolve<UpdateSiteHandler>().Handle(new UpdateSiteCommand(site.Id, Input() with { Name = "Warehouse", City = "Sharjah" }), default);

        var fetched = (await Resolve<GetSiteHandler>().Handle(new GetSiteQuery(site.Id), default)).Value;
        fetched.Name.ShouldBe("Warehouse");
        fetched.City.ShouldBe("Sharjah");
    }

    [Fact]
    public async Task Deactivated_sites_are_hidden_unless_requested()
    {
        var customerId = await GivenCustomer();
        var site = await GivenSite(customerId);

        await Resolve<DeactivateSiteHandler>().Handle(new DeactivateSiteCommand(site.Id), default);

        (await Resolve<ListSitesHandler>().Handle(new ListSitesQuery(customerId), default)).Value.ShouldBeEmpty();
        (await Resolve<ListSitesHandler>().Handle(new ListSitesQuery(customerId, IncludeInactive: true), default)).Value.Single().IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Adding_a_site_to_an_unknown_customer_returns_not_found()
    {
        var result = await Resolve<AddSiteHandler>().Handle(new AddSiteCommand(Guid.CreateVersion7(), Input()), default);

        result.Error.ShouldBe(CustomerErrors.NotFound);
    }

    [Fact(Skip = "Enabled in Phase 4 once work orders exist (US-SITE-02).")]
    public Task Site_with_open_work_orders_cannot_be_deactivated() => Task.CompletedTask;
}
