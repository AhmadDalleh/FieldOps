using FieldOps.Domain.Inventory;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

internal sealed class StockLocationConfiguration : IEntityTypeConfiguration<StockLocation>
{
    public void Configure(EntityTypeBuilder<StockLocation> builder)
    {
        builder.ToTable("stock_locations");
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(l => l.TechnicianId).IsUnique();
        builder.HasOne<Technician>().WithOne().HasForeignKey<StockLocation>(l => l.TechnicianId).OnDelete(DeleteBehavior.Restrict);
    }
}
