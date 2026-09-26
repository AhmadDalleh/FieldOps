using FieldOps.Domain.Common;

namespace FieldOps.Domain.Inventory;

public enum StockLocationType { Warehouse, Van }

public sealed class StockLocation : AuditableEntity
{
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

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
