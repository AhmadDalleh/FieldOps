using FieldOps.Application.Features.Assets;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Sites;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Assets;
using FieldOps.Domain.Customers;
using FieldOps.Domain.Identity;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Assets;

public class AssetTests(PostgresFixture fixture) : TestBase(fixture)
{
    private static AssetInput Input(string? serial = "SN-100", string? manufacturer = "Daikin", DateOnly? warranty = null) =>
        new(AssetType.AC, "Split AC - lobby", manufacturer, "FTXM35", serial, new DateOnly(2025, 6, 1), warranty);

    private async Task<(Guid CustomerId, Guid SiteId)> GivenSite()
    {
        var customer = (await Resolve<CreateCustomerHandler>().Handle(new CreateCustomerCommand(
            new CustomerInput("Acme", CustomerType.Business, null, "+971500000000", null, null, null), Force: true), default)).Value;
        var site = (await Resolve<AddSiteHandler>().Handle(new AddSiteCommand(customer.Id,
            new SiteInput("HQ", "Sheikh Zayed Road", null, "Dubai", null, null, null, null, null)), default)).Value;
        return (customer.Id, site.Id);
    }

    private Task<Domain.Common.Result<AssetDto>> Register(Guid siteId, AssetInput input) =>
        Resolve<RegisterAssetHandler>().Handle(new RegisterAssetCommand(siteId, input), default);

    [Fact]
    public async Task Asset_is_registered_with_make_model_serial_and_dates()
    {
        var (_, siteId) = await GivenSite();

        var asset = (await Register(siteId, Input(warranty: new DateOnly(2027, 6, 1)))).Value;

        asset.SiteName.ShouldBe("HQ");
        asset.Manufacturer.ShouldBe("Daikin");
        asset.Model.ShouldBe("FTXM35");
        asset.SerialNumber.ShouldBe("SN-100");
        asset.InstallDate.ShouldBe(new DateOnly(2025, 6, 1));
        asset.WarrantyExpiresOn.ShouldBe(new DateOnly(2027, 6, 1));
        asset.Status.ShouldBe(AssetStatus.Active);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("Chiller 1", true)]
    public void Name_is_required(string name, bool valid)
    {
        new AssetInputValidator().Validate(Input() with { Name = name }).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Type_must_be_a_known_asset_type()
    {
        new AssetInputValidator().Validate(Input() with { AssetType = (AssetType)99 }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Asset_cannot_be_added_to_a_site_of_an_inactive_customer()
    {
        var (customerId, siteId) = await GivenSite();
        await Resolve<DeactivateCustomerHandler>().Handle(new DeactivateCustomerCommand(customerId), default);

        var result = await Register(siteId, Input());

        result.Error.ShouldBe(AssetErrors.SiteNotAvailable);
    }

    [Fact]
    public async Task Asset_cannot_be_added_to_an_inactive_site()
    {
        var (_, siteId) = await GivenSite();
        await Resolve<DeactivateSiteHandler>().Handle(new DeactivateSiteCommand(siteId), default);

        (await Register(siteId, Input())).Error.ShouldBe(AssetErrors.SiteNotAvailable);
    }

    [Fact]
    public async Task Unknown_site_returns_not_found()
    {
        (await Register(Guid.CreateVersion7(), Input())).Error.ShouldBe(CustomerErrors.SiteNotFound);
    }

    [Fact]
    public async Task Same_manufacturer_and_serial_number_returns_a_validation_error()
    {
        var (_, siteId) = await GivenSite();
        await Register(siteId, Input("SN-100", "Daikin"));

        var result = await Register(siteId, Input("sn-100", "DAIKIN"));

        result.Error.ShouldBe(AssetErrors.DuplicateSerial);
        (await NewDb().Assets.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Same_serial_number_from_a_different_manufacturer_is_allowed()
    {
        var (_, siteId) = await GivenSite();
        await Register(siteId, Input("SN-100", "Daikin"));

        (await Register(siteId, Input("SN-100", "Carrier"))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Assets_without_serial_numbers_are_never_duplicates()
    {
        var (_, siteId) = await GivenSite();
        await Register(siteId, Input(serial: null));

        (await Register(siteId, Input(serial: null))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Editing_an_asset_keeps_its_own_serial_but_rejects_another_assets_serial()
    {
        var (_, siteId) = await GivenSite();
        var first = (await Register(siteId, Input("SN-1"))).Value;
        await Register(siteId, Input("SN-2"));

        var keep = await Resolve<UpdateAssetHandler>().Handle(new UpdateAssetCommand(first.Id, Input("SN-1") with { Name = "Renamed" }), default);
        var clash = await Resolve<UpdateAssetHandler>().Handle(new UpdateAssetCommand(first.Id, Input("SN-2")), default);

        keep.Value.Name.ShouldBe("Renamed");
        clash.Error.ShouldBe(AssetErrors.DuplicateSerial);
    }

    [Theory]
    [InlineData("2026-10-02", true)]
    [InlineData("2026-10-01", false)]
    public async Task Warranty_flag_uses_the_Dubai_date(string expires, bool underWarranty)
    {
        // 2026-10-01 22:00 UTC is already 2026-10-02 02:00 in Dubai.
        Fixture.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 1, 22, 0, 0, TimeSpan.Zero));
        var (_, siteId) = await GivenSite();

        var asset = (await Register(siteId, Input(warranty: DateOnly.Parse(expires)))).Value;

        asset.UnderWarranty.ShouldBe(underWarranty);
    }

    [Fact]
    public async Task Customer_assets_list_covers_all_of_its_sites()
    {
        var (customerId, siteId) = await GivenSite();
        var secondSite = (await Resolve<AddSiteHandler>().Handle(new AddSiteCommand(customerId,
            new SiteInput("Warehouse", "Al Quoz", null, "Dubai", null, null, null, null, null)), default)).Value;
        await Register(siteId, Input("A"));
        await Register(secondSite.Id, Input("B"));

        var assets = (await Resolve<ListCustomerAssetsHandler>().Handle(new ListCustomerAssetsQuery(customerId), default)).Value;
        var siteAssets = (await Resolve<ListSiteAssetsHandler>().Handle(new ListSiteAssetsQuery(siteId), default)).Value;

        assets.Select(a => a.SiteName).ShouldBe(["HQ", "Warehouse"]);
        siteAssets.Single().SerialNumber.ShouldBe("A");
    }

    [Fact]
    public async Task Asset_history_is_empty_until_work_orders_exist()
    {
        var (_, siteId) = await GivenSite();
        var asset = (await Register(siteId, Input())).Value;
        var dispatcher = await GivenUser(Role.Dispatcher);
        Fixture.CurrentUser.SignInAs(dispatcher.Id, Role.Dispatcher);

        var history = await Resolve<GetAssetHistoryHandler>().Handle(new GetAssetHistoryQuery(asset.Id), default);

        history.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task Technician_without_a_work_order_on_the_asset_cannot_see_its_history()
    {
        var (_, siteId) = await GivenSite();
        var asset = (await Register(siteId, Input())).Value;
        var tech = await GivenUser(Role.Technician);
        Fixture.CurrentUser.SignInAs(tech.Id, Role.Technician);

        var history = await Resolve<GetAssetHistoryHandler>().Handle(new GetAssetHistoryQuery(asset.Id), default);

        history.Error.Type.ShouldBe(Domain.Common.ErrorType.Forbidden);
    }

    [Fact]
    public async Task History_lists_the_assets_work_orders_newest_first_and_technicians_see_their_own()
    {
        var (customerId, siteId) = await GivenSite();
        var asset = (await Register(siteId, Input())).Value;
        var (techUser, tech) = await GivenTechnician();
        var first = await GivenWorkOrder(customerId, siteId, asset.Id, title: "First visit");
        await Complete(first.Id, tech, "Cleaned coil");
        Fixture.Clock.Advance(TimeSpan.FromDays(1));
        var second = await GivenWorkOrder(customerId, siteId, asset.Id, WorkOrderType.Maintenance, title: "Second visit");
        await GivenWorkOrder(customerId, siteId, title: "Not on this asset");

        var history = (await Resolve<GetAssetHistoryHandler>().Handle(new GetAssetHistoryQuery(asset.Id), default)).Value;

        history.Select(h => h.WorkOrderId).ShouldBe([second.Id, first.Id]);
        history[0].Type.ShouldBe(WorkOrderType.Maintenance);
        history[0].Status.ShouldBe(WorkOrderStatus.New);
        history[1].WorkOrderNumber.ShouldBe(first.Number);
        history[1].TechnicianName.ShouldBe("Technician User");
        history[1].CompletionNotes.ShouldBe("Cleaned coil");
        history[1].Status.ShouldBe(WorkOrderStatus.Completed);

        Fixture.CurrentUser.SignInAs(techUser, Role.Technician, tech);
        (await Resolve<GetAssetHistoryHandler>().Handle(new GetAssetHistoryQuery(asset.Id), default)).Value.Count.ShouldBe(2);
        (await Resolve<GetAssetHandler>().Handle(new GetAssetQuery(asset.Id), default)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Technician_cannot_read_an_asset_that_is_not_on_their_jobs()
    {
        var (_, siteId) = await GivenSite();
        var asset = (await Register(siteId, Input())).Value;
        var (techUser, tech) = await GivenTechnician();
        Fixture.CurrentUser.SignInAs(techUser, Role.Technician, tech);

        (await Resolve<GetAssetHandler>().Handle(new GetAssetQuery(asset.Id), default)).Error.Type
            .ShouldBe(Domain.Common.ErrorType.Forbidden);
    }
}
