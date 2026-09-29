using FieldOps.Application.Features.Assets;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Assets;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Application.Tests.WorkOrders;

public class EditWorkOrderTests(PostgresFixture fixture) : TestBase(fixture)
{
    private static UpdateWorkOrderInput Input(WorkOrderDto wo, string title = "Replace compressor", Guid? assetId = null) =>
        new(title, "Updated", WorkOrderType.Installation, WorkOrderPriority.Urgent, PostgresFixture.Start.AddDays(2), assetId, wo.Version);

    private Task<Domain.Common.Result<WorkOrderDto>> Update(Guid id, UpdateWorkOrderInput input) =>
        Resolve<UpdateWorkOrderHandler>().Handle(new UpdateWorkOrderCommand(id, input), default);

    private async Task<WorkOrderDto> Get(Guid id) =>
        (await Resolve<GetWorkOrderHandler>().Handle(new GetWorkOrderQuery(id), default)).Value;

    [Fact]
    public async Task Office_edits_title_description_priority_type_due_and_asset()
    {
        var (c, s) = await GivenCustomerAndSite();
        var asset = (await Resolve<RegisterAssetHandler>().Handle(new RegisterAssetCommand(s,
            new AssetInput(AssetType.Chiller, "Chiller 1", null, null, null, null, null)), default)).Value;
        var wo = await GivenWorkOrder(c, s);

        var updated = (await Update(wo.Id, Input(wo, assetId: asset.Id))).Value;

        updated.Title.ShouldBe("Replace compressor");
        updated.Description.ShouldBe("Updated");
        updated.Type.ShouldBe(WorkOrderType.Installation);
        updated.Priority.ShouldBe(WorkOrderPriority.Urgent);
        updated.DueBy.ShouldBe(PostgresFixture.Start.AddDays(2));
        updated.Asset!.Name.ShouldBe("Chiller 1");
        updated.Version.ShouldNotBe(wo.Version);
    }

    [Fact]
    public async Task Editing_is_blocked_once_work_has_started()
    {
        var (c, s) = await GivenCustomerAndSite();
        var (_, tech) = await GivenTechnician();
        var wo = await GivenWorkOrder(c, s);
        await Assign(wo.Id, tech);
        await Advance(wo.Id, (w, u, now) => w.Dispatch(u, now));
        await Advance(wo.Id, (w, u, now) => w.Start(u, now));
        var current = await Get(wo.Id);

        (await Update(wo.Id, Input(current))).Error.ShouldBe(WorkOrderErrors.NotEditable);
        current.IsEditable.ShouldBeFalse();
    }

    [Fact]
    public async Task Editing_while_on_hold_is_allowed()
    {
        var (c, s) = await GivenCustomerAndSite();
        var (_, tech) = await GivenTechnician();
        var wo = await GivenWorkOrder(c, s);
        await Assign(wo.Id, tech);
        await Advance(wo.Id, (w, u, now) => w.Dispatch(u, now));
        await Advance(wo.Id, (w, u, now) => w.Start(u, now));
        await Advance(wo.Id, (w, u, now) => w.Hold("Parts", u, now));

        (await Update(wo.Id, Input(await Get(wo.Id)))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Second_writer_with_the_old_version_gets_a_conflict()
    {
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);

        (await Update(wo.Id, Input(wo, "First"))).IsSuccess.ShouldBeTrue();
        var second = await Update(wo.Id, Input(wo, "Second"));

        second.Error.ShouldBe(WorkOrderErrors.ConcurrencyConflict);
        (await Get(wo.Id)).Title.ShouldBe("First");
    }

    [Fact]
    public async Task Concurrent_save_between_read_and_write_is_caught_by_xmin()
    {
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);

        // Load the row in one context, change it in another, then save the first.
        var db = NewDb();
        var stale = db.WorkOrders.Single(w => w.Id == wo.Id);
        await Update(wo.Id, Input(wo, "Someone else"));
        stale.Update(new WorkOrderDetails("Mine", null, WorkOrderType.Repair, WorkOrderPriority.Low, null, null));

        (await db.SaveAsync(default)).Error.ShouldBe(WorkOrderErrors.ConcurrencyConflict);
    }

    [Fact]
    public async Task Asset_must_belong_to_the_work_orders_site()
    {
        var (c, s) = await GivenCustomerAndSite();
        var (_, otherSite) = await GivenCustomerAndSite("Other");
        var foreign = (await Resolve<RegisterAssetHandler>().Handle(new RegisterAssetCommand(otherSite,
            new AssetInput(AssetType.AC, "AC", null, null, null, null, null)), default)).Value;
        var wo = await GivenWorkOrder(c, s);

        (await Update(wo.Id, Input(wo, assetId: foreign.Id))).Error.ShouldBe(WorkOrderErrors.AssetNotOfSite);
    }

    [Fact]
    public async Task Missing_work_order_returns_not_found()
    {
        await SignInOffice();
        (await Update(Guid.NewGuid(), new UpdateWorkOrderInput("X", null, WorkOrderType.Repair, WorkOrderPriority.Low, null, null, 0)))
            .Error.ShouldBe(WorkOrderErrors.NotFound);
    }
}
