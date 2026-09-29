using FieldOps.Domain.Inventory;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class StockTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid PartId = Guid.NewGuid();

    private static StockLevel Level(decimal quantity, Guid? location = null)
    {
        var level = new StockLevel(PartId, location ?? Guid.NewGuid());
        if (quantity > 0) StockLedger.Receive(level, quantity, User, Now);
        return level;
    }

    private static Part NewPart(decimal price = 12.50m) =>
        Part.Create(new PartDetails(" flt-01 ", "Air filter", null, PartUnit.Pcs, 5m, price, 4m)).Value;

    [Fact]
    public void Receive_adds_stock_and_records_where_it_went()
    {
        var level = Level(0);

        var movement = StockLedger.Receive(level, 10, User, Now).Value;

        level.Quantity.ShouldBe(10);
        (movement.Type, movement.FromLocationId, movement.ToLocationId, movement.Quantity)
            .ShouldBe((StockMovementType.Receive, (Guid?)null, (Guid?)level.StockLocationId, 10m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.005)]
    public void Quantities_must_be_positive_with_two_decimals(decimal quantity)
    {
        StockLedger.Receive(Level(0), quantity, User, Now).Error.ShouldBe(InventoryErrors.InvalidQuantity);
    }

    [Fact]
    public void Transfer_moves_stock_atomically_and_never_below_zero()
    {
        var warehouse = Level(5);
        var van = Level(0);

        StockLedger.Transfer(warehouse, van, 6, User, Now).Error.Code.ShouldBe("Stock.Insufficient");
        (warehouse.Quantity, van.Quantity).ShouldBe((5m, 0m));

        var movement = StockLedger.Transfer(warehouse, van, 3, User, Now).Value;
        (warehouse.Quantity, van.Quantity).ShouldBe((2m, 3m));
        (movement.FromLocationId, movement.ToLocationId).ShouldBe((warehouse.StockLocationId, van.StockLocationId));
    }

    [Fact]
    public void Transfer_to_the_same_location_is_rejected()
    {
        var location = Guid.NewGuid();
        StockLedger.Transfer(Level(5, location), Level(0, location), 1, User, Now).Error.ShouldBe(InventoryErrors.SameLocation);
    }

    [Fact]
    public void Adjust_sets_the_count_and_records_the_difference()
    {
        var level = Level(10);

        var down = StockLedger.Adjust(level, 7.5m, "Counted", User, Now).Value;
        (down.FromLocationId, down.ToLocationId, down.Quantity).ShouldBe(((Guid?)level.StockLocationId, (Guid?)null, 2.5m));
        var up = StockLedger.Adjust(level, 9, "Found a box", User, Now).Value;
        (up.ToLocationId, up.Quantity).ShouldBe(((Guid?)level.StockLocationId, 1.5m));
        level.Quantity.ShouldBe(9);

        StockLedger.Adjust(level, -1, "x", User, Now).Error.ShouldBe(InventoryErrors.InvalidQuantity);
        StockLedger.Adjust(level, 9, "x", User, Now).Error.ShouldBe(InventoryErrors.NoChange);
        StockLedger.Adjust(level, 3, " ", User, Now).Error.ShouldBe(InventoryErrors.ReasonRequired);
    }

    [Fact]
    public void Parts_normalise_the_sku_and_reject_negative_amounts()
    {
        NewPart().Sku.ShouldBe("FLT-01");
        Part.Create(new PartDetails("A", "B", null, PartUnit.M, -1, 0, 0)).Error.ShouldBe(InventoryErrors.NegativeAmount);
        NewPart().IsLow(3).ShouldBeTrue();
        NewPart().IsLow(4).ShouldBeFalse();
    }

    private static WorkOrder StartedJob(bool start = true)
    {
        var wo = WorkOrder.Create("WO-000001", Guid.NewGuid(), Guid.NewGuid(),
            new WorkOrderDetails("AC", null, WorkOrderType.Repair, WorkOrderPriority.Medium, null, null), [], User, Now);
        wo.Schedule(Guid.NewGuid(), Now, Now.AddHours(1), User, Now);
        wo.Dispatch(User, Now);
        if (start) wo.Start(User, Now);
        return wo;
    }

    [Fact]
    public void Using_a_part_takes_it_from_the_van_and_snapshots_the_price()
    {
        var part = NewPart(price: 12.50m);
        var van = new StockLevel(part.Id, Guid.NewGuid());
        StockLedger.Receive(van, 3, User, Now);
        var job = StartedJob();

        var (line, movement) = WorkOrderPart.Use(job, part, van, 2, User, Now).Value;

        van.Quantity.ShouldBe(1);
        line.UnitPrice.ShouldBe(12.50m);
        line.LineTotal.ShouldBe(25m);
        line.StockMovementId.ShouldBe(movement.Id);
        (movement.Type, movement.WorkOrderId).ShouldBe((StockMovementType.Consume, (Guid?)job.Id));

        part.Update(new PartDetails("FLT-01", "Air filter", null, PartUnit.Pcs, 5m, 20m, 4m));
        line.UnitPrice.ShouldBe(12.50m);
    }

    [Fact]
    public void Using_more_than_the_van_holds_fails_and_keeps_the_stock()
    {
        var part = NewPart();
        var van = new StockLevel(part.Id, Guid.NewGuid());
        StockLedger.Receive(van, 2, User, Now);

        WorkOrderPart.Use(StartedJob(), part, van, 3, User, Now).Error.Code.ShouldBe("Stock.Insufficient");
        van.Quantity.ShouldBe(2);
    }

    [Fact]
    public void Parts_are_recorded_only_once_the_job_is_started()
    {
        var part = NewPart();
        var van = new StockLevel(part.Id, Guid.NewGuid());
        StockLedger.Receive(van, 2, User, Now);

        WorkOrderPart.Use(StartedJob(start: false), part, van, 1, User, Now).Error.Code.ShouldBe("WorkOrder.PartsNotAllowedNow");
    }

    [Fact]
    public void Removing_a_line_returns_the_parts_to_the_van_until_completion()
    {
        var part = NewPart();
        var van = new StockLevel(part.Id, Guid.NewGuid());
        StockLedger.Receive(van, 2, User, Now);
        var job = StartedJob();
        var (line, _) = WorkOrderPart.Use(job, part, van, 2, User, Now).Value;

        var returned = line.ReturnTo(van, job, User, Now).Value;

        van.Quantity.ShouldBe(2);
        (returned.Type, returned.ToLocationId).ShouldBe((StockMovementType.Return, (Guid?)van.StockLocationId));
        job.Complete("Done", "Sara", Guid.NewGuid(), User, Now);
        line.ReturnTo(van, job, User, Now).Error.ShouldBe(WorkOrderErrors.PartsLocked);
    }
}
