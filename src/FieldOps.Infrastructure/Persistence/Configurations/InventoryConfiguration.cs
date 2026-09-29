using FieldOps.Domain.Inventory;
using FieldOps.Domain.WorkOrders;
using FieldOps.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

internal sealed class PartConfiguration : IEntityTypeConfiguration<Part>
{
    public void Configure(EntityTypeBuilder<Part> builder)
    {
        builder.ToTable("parts");
        builder.Property(p => p.Sku).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.UnitCost).HasPrecision(18, 2);
        builder.Property(p => p.UnitPrice).HasPrecision(18, 2);
        builder.Property(p => p.ReorderLevel).HasPrecision(10, 2);
        builder.HasIndex(p => p.Sku).IsUnique();
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_parts_unit_cost", "unit_cost >= 0");
            t.HasCheckConstraint("ck_parts_unit_price", "unit_price >= 0");
        });
    }
}

internal sealed class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> builder)
    {
        builder.ToTable("stock_levels", t => t.HasCheckConstraint("ck_stock_levels_quantity", "quantity >= 0"));
        builder.HasKey(l => new { l.PartId, l.StockLocationId });
        builder.Property(l => l.Quantity).HasPrecision(10, 2);
        builder.Property(l => l.Version).IsRowVersion();
        builder.HasIndex(l => l.StockLocationId);
        builder.HasOne<Part>().WithMany().HasForeignKey(l => l.PartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(l => l.StockLocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements", t => t.HasCheckConstraint("ck_stock_movements_quantity", "quantity > 0"));
        builder.Property(m => m.Quantity).HasPrecision(10, 2);
        builder.Property(m => m.Reason).HasMaxLength(500);
        builder.HasIndex(m => new { m.PartId, m.CreatedAt });
        builder.HasIndex(m => m.WorkOrderId);
        builder.HasOne<Part>().WithMany().HasForeignKey(m => m.PartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(m => m.FromLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(m => m.ToLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkOrder>().WithMany().HasForeignKey(m => m.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(m => m.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkOrderPartConfiguration : IEntityTypeConfiguration<WorkOrderPart>
{
    public void Configure(EntityTypeBuilder<WorkOrderPart> builder)
    {
        builder.ToTable("work_order_parts", t => t.HasCheckConstraint("ck_work_order_parts_quantity", "quantity > 0"));
        builder.Property(p => p.Quantity).HasPrecision(10, 2);
        builder.Property(p => p.UnitPrice).HasPrecision(18, 2);
        builder.Ignore(p => p.LineTotal);
        builder.HasIndex(p => p.WorkOrderId);
        builder.HasOne<WorkOrder>().WithMany().HasForeignKey(p => p.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Part>().WithMany().HasForeignKey(p => p.PartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockLocation>().WithMany().HasForeignKey(p => p.StockLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockMovement>().WithMany().HasForeignKey(p => p.StockMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}
