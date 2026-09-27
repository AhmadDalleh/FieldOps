using FieldOps.Application.Common;
using FieldOps.Application.Features.Inventory;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Inventory;

public class InventoryTests(PostgresFixture fixture) : TestBase(fixture)
{
    private static readonly Guid Warehouse = StockLocation.MainWarehouseId;

    private async Task<PartDto> GivenPart(string sku = "FLT-01", decimal reorder = 5) =>
        (await Resolve<CreatePartHandler>().Handle(new CreatePartCommand(
            new PartInput(sku, "Air filter", null, PartUnit.Pcs, 8m, 15m, reorder)), default)).Value;

    private Task<Result<StockRow>> Receive(Guid partId, decimal quantity, Guid? location = null) =>
        Resolve<ReceiveStockHandler>().Handle(new ReceiveStockCommand(new ReceiveInput(partId, location ?? Warehouse, quantity)), default);

    private Task<Result<StockRow>> Transfer(Guid partId, Guid from, Guid to, decimal quantity) =>
        Resolve<TransferStockHandler>().Handle(new TransferStockCommand(new TransferInput(partId, from, to, quantity)), default);

    private async Task<Guid> VanOf(Guid technicianId) =>
        await NewDb().StockLocations.Where(l => l.TechnicianId == technicianId).Select(l => l.Id).SingleAsync();

    [Fact]
    public async Task Parts_have_unique_skus_ignoring_case_and_spaces()
    {
        await SignInOffice(Role.Admin);
        var part = await GivenPart();

        part.Sku.ShouldBe("FLT-01");
        (await Resolve<CreatePartHandler>().Handle(new CreatePartCommand(
            new PartInput(" flt-01", "Other", null, PartUnit.Pcs, 1, 1, 0)), default)).Error.ShouldBe(InventoryErrors.DuplicateSku);
        var renamed = await Resolve<UpdatePartHandler>().Handle(new UpdatePartCommand(part.Id,
            new PartInput("FLT-01", "Air filter 20x20", "Pleated", PartUnit.Pcs, 8m, 16m, 5)), default);
        renamed.Value.UnitPrice.ShouldBe(16m);
    }

    [Fact]
    public async Task Receive_goes_into_a_warehouse_and_shows_on_the_stock_list()
    {
        await SignInOffice(Role.Admin);
        var part = await GivenPart(reorder: 5);
        var (_, tech) = await GivenTechnician();

        (await Receive(part.Id, 3, await VanOf(tech))).Error.ShouldBe(InventoryErrors.ReceiveIntoWarehouse);
        var row = (await Receive(part.Id, 3)).Value;

        row.Total.ShouldBe(3);
        row.IsLow.ShouldBeTrue();
        row.Levels.ShouldHaveSingleItem().ShouldBe(new StockAt(Warehouse, 3));
        (await Resolve<GetStockHandler>().Handle(new GetStockQuery(LowOnly: true), default)).ShouldHaveSingleItem();
        (await Resolve<ListPartsHandler>().Handle(new ListPartsQuery(new PageRequest(), LowOnly: true), default)).Items
            .ShouldHaveSingleItem().TotalQuantity.ShouldBe(3);
        (await Receive(part.Id, 10)).Value.IsLow.ShouldBeFalse();
    }

    [Fact]
    public async Task Transfer_moves_stock_to_a_van_and_refuses_more_than_the_source_holds()
    {
        await SignInOffice(Role.Admin);
        var part = await GivenPart();
        var (_, tech) = await GivenTechnician();
        var van = await VanOf(tech);
        await Receive(part.Id, 10);

        (await Transfer(part.Id, Warehouse, van, 11)).Error.Code.ShouldBe("Stock.Insufficient");
        var row = (await Transfer(part.Id, Warehouse, van, 4)).Value;

        row.Total.ShouldBe(10);
        row.Levels.OrderBy(l => l.Quantity).ShouldBe([new StockAt(van, 4), new StockAt(Warehouse, 6)]);
        (await Resolve<GetStockHandler>().Handle(new GetStockQuery(van), default)).Single().Levels.ShouldHaveSingleItem().Quantity.ShouldBe(4);
    }

    [Fact]
    public async Task Adjust_sets_the_count_and_every_change_is_in_the_history()
    {
        var admin = await SignInOffice(Role.Admin);
        var part = await GivenPart();
        var (_, tech) = await GivenTechnician();
        var van = await VanOf(tech);
        await Receive(part.Id, 10);
        await Transfer(part.Id, Warehouse, van, 4);
        Fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        var adjusted = await Resolve<AdjustStockHandler>().Handle(new AdjustStockCommand(
            new AdjustInput(part.Id, Warehouse, 5, "Stock count")), default);
        adjusted.Value.Total.ShouldBe(9);

        var history = (await Resolve<ListMovementsHandler>().Handle(new ListMovementsQuery(new PageRequest(), part.Id), default)).Items;
        history.Select(m => m.Type).ShouldBe([StockMovementType.Adjust, StockMovementType.Transfer, StockMovementType.Receive]);
        history[0].ShouldSatisfyAllConditions(
            m => m.Quantity.ShouldBe(1),
            m => m.FromLocation.ShouldBe("Main warehouse"),
            m => m.Reason.ShouldBe("Stock count"),
            m => m.CreatedByName.ShouldBe("Admin User"));
        history[1].ToLocation.ShouldStartWith("Van - ");
        (await Resolve<ListMovementsHandler>().Handle(new ListMovementsQuery(new PageRequest(), LocationId: van), default))
            .Items.ShouldHaveSingleItem().Type.ShouldBe(StockMovementType.Transfer);
        _ = admin;
    }
}
