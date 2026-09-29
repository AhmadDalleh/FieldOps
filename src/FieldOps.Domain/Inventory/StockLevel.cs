namespace FieldOps.Domain.Inventory;

/// <summary>How much of a part is at one location. Only <see cref="StockLedger"/> changes it, and it never goes negative.</summary>
public sealed class StockLevel
{
    private StockLevel() { }

    public StockLevel(Guid partId, Guid stockLocationId)
    {
        PartId = partId;
        StockLocationId = stockLocationId;
    }

    public Guid PartId { get; private init; }
    public Guid StockLocationId { get; private init; }
    public decimal Quantity { get; internal set; }

    /// <summary>Maps to the Postgres <c>xmin</c> column, so two people taking the last unit cannot both succeed.</summary>
    public uint Version { get; private set; }
}
