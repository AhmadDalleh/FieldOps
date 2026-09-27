using FieldOps.Domain.Common;

namespace FieldOps.Domain.Inventory;

public enum StockLocationType { Warehouse, Van }

public sealed class StockLocation : AuditableEntity
{
    /// <summary>The main warehouse, created with the database; stock is received here (US-INV-03).</summary>
    public static readonly Guid MainWarehouseId = new("01a0e000-0000-7000-8000-000000000001");

    private StockLocation() { }

    public string Name { get; private set; } = null!;
    public StockLocationType Type { get; private init; }
    public Guid? TechnicianId { get; private init; }
    public bool IsActive { get; private set; } = true;

    public static StockLocation CreateVan(Guid technicianId, string technicianName) => new()
    {
        Name = $"Van - {technicianName}",
        Type = StockLocationType.Van,
        TechnicianId = technicianId,
    };

    public static StockLocation CreateMainWarehouse() => new()
    {
        Id = MainWarehouseId,
        Name = "Main warehouse",
        Type = StockLocationType.Warehouse,
    };

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
