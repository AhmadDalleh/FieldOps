using FieldOps.Application.Common;
using FieldOps.Application.Features.Inventory;
using FieldOps.Application.Features.Me;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Inventory;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.WorkOrders;

public class WorkOrderPartTests(PostgresFixture fixture) : TestBase(fixture)
{
    private (Guid UserId, Guid TechnicianId) _tech;
    private Guid _van;
    private Guid _customer;
    private Guid _site;

    /// <summary>A technician whose van holds <paramref name="vanStock"/> of a new part priced 15.00.</summary>
    private async Task<PartDto> Given(decimal vanStock)
    {
        await SignInOffice(Role.Admin);
        (_customer, _site) = await GivenCustomerAndSite();
        _tech = await GivenTechnician();
        _van = await NewDb().StockLocations.Where(l => l.TechnicianId == _tech.TechnicianId).Select(l => l.Id).SingleAsync();
        var part = (await Resolve<CreatePartHandler>().Handle(new CreatePartCommand(
            new PartInput("CAP-35", "Run capacitor 35uF", null, PartUnit.Pcs, 9m, 15m, 2)), default)).Value;
        if (vanStock > 0)
        {
            await Resolve<ReceiveStockHandler>().Handle(new ReceiveStockCommand(
                new ReceiveInput(part.Id, StockLocation.MainWarehouseId, vanStock)), default);
            await Resolve<TransferStockHandler>().Handle(new TransferStockCommand(
                new TransferInput(part.Id, StockLocation.MainWarehouseId, _van, vanStock)), default);
        }
        return part;
    }

    private async Task<Guid> StartedJob()
    {
        await SignInOffice();
        var wo = await GivenWorkOrder(_customer, _site);
        await Assign(wo.Id, _tech.TechnicianId);
        await Advance(wo.Id, (w, u, now) => w.Dispatch(u, now));
        await Advance(wo.Id, (w, u, now) => w.Start(u, now));
        SignInTechnician(_tech);
        return wo.Id;
    }

    private Task<Result<IReadOnlyList<WorkOrderPartDto>>> Use(Guid workOrderId, Guid partId, decimal quantity) =>
        Resolve<AddWorkOrderPartHandler>().Handle(new AddWorkOrderPartCommand(workOrderId, new UsePartInput(partId, quantity)), default);

    private async Task<decimal> VanQuantity(Guid partId) =>
        await NewDb().StockLevels.Where(l => l.PartId == partId && l.StockLocationId == _van).Select(l => l.Quantity).SingleOrDefaultAsync();

    [Fact]
    public async Task Van_stock_lists_only_parts_in_my_van()
    {
        var part = await Given(vanStock: 3);
        await Resolve<CreatePartHandler>().Handle(new CreatePartCommand(
            new PartInput("NONE", "Not in the van", null, PartUnit.Pcs, 1, 1, 0)), default);
        SignInTechnician(_tech);

        var stock = (await Resolve<VanStockHandler>().Handle(new VanStockQuery(), default)).Value;

        stock.ShouldHaveSingleItem().ShouldBe(new VanStockItem(part.Id, "CAP-35", "Run capacitor 35uF", PartUnit.Pcs, 3, 15m));
    }

    [Fact]
    public async Task Using_a_part_consumes_van_stock_and_snapshots_the_price_in_one_save()
    {
        var part = await Given(vanStock: 3);
        var job = await StartedJob();

        var lines = (await Use(job, part.Id, 2)).Value;

        lines.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            l => l.Quantity.ShouldBe(2),
            l => l.UnitPrice.ShouldBe(15m),
            l => l.LineTotal.ShouldBe(30m),
            l => l.LocationName.ShouldStartWith("Van - "),
            l => l.CanRemove.ShouldBeTrue());
        (await VanQuantity(part.Id)).ShouldBe(1);
        var consume = await NewDb().StockMovements.SingleAsync(m => m.Type == StockMovementType.Consume);
        (consume.WorkOrderId, consume.FromLocationId, consume.Quantity).ShouldBe(((Guid?)job, (Guid?)_van, 2m));
    }

    [Fact]
    public async Task Adding_part_with_quantity_above_van_stock_returns_conflict_and_keeps_stock()
    {
        var part = await Given(vanStock: 2);
        var job = await StartedJob();

        (await Use(job, part.Id, 3)).Error.Code.ShouldBe("Stock.Insufficient");

        (await VanQuantity(part.Id)).ShouldBe(2);
        (await NewDb().WorkOrderParts.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Two_people_taking_the_last_unit_at_once_end_with_one_success_and_one_conflict()
    {
        var part = await Given(vanStock: 1);
        var first = await StartedJob();
        var second = await StartedJob();

        var results = await Task.WhenAll(Use(first, part.Id, 1), Use(second, part.Id, 1));

        results.Count(r => r.IsSuccess).ShouldBe(1);
        results.Single(r => r.IsFailure).Error.Code.ShouldBeOneOf("Stock.Insufficient", "Stock.ConcurrencyConflict");
        (await VanQuantity(part.Id)).ShouldBe(0);
        (await NewDb().WorkOrderParts.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Removing_a_line_returns_the_parts_and_cancel_needs_them_returned()
    {
        var part = await Given(vanStock: 3);
        var job = await StartedJob();
        var line = (await Use(job, part.Id, 2)).Value.Single();

        await SignInOffice();
        var cancel = new CancelWorkOrderCommand(job, new CancelInput("Customer changed their mind"));
        (await Resolve<CancelWorkOrderHandler>().Handle(cancel, default)).Error.ShouldBe(WorkOrderErrors.PartsNotReturned);

        (await Resolve<RemoveWorkOrderPartHandler>().Handle(new RemoveWorkOrderPartCommand(job, line.Id), default)).Value.ShouldBeEmpty();
        (await VanQuantity(part.Id)).ShouldBe(3);
        (await NewDb().StockMovements.CountAsync(m => m.Type == StockMovementType.Return && m.WorkOrderId == job)).ShouldBe(1);
        (await Resolve<CancelWorkOrderHandler>().Handle(cancel, default)).Value.Status.ShouldBe(WorkOrderStatus.Cancelled);
    }

    [Fact]
    public async Task Parts_are_locked_after_completion_and_other_technicians_are_kept_out()
    {
        var part = await Given(vanStock: 3);
        var job = await StartedJob();
        var line = (await Use(job, part.Id, 1)).Value.Single();

        SignInTechnician(await GivenTechnician());
        (await Use(job, part.Id, 1)).Error.ShouldBe(Errors.Forbidden);

        SignInTechnician(_tech);
        var signature = await GivenSignature(job);
        await Advance(job, (w, u, now) => w.Complete("Done", "Sara", signature, u, now));
        (await Resolve<RemoveWorkOrderPartHandler>().Handle(new RemoveWorkOrderPartCommand(job, line.Id), default)).Error
            .ShouldBe(WorkOrderErrors.PartsLocked);
        (await Use(job, part.Id, 1)).Error.Code.ShouldBe("WorkOrder.PartsNotAllowedNow");
    }
}
