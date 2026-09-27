using FieldOps.Domain.Assets;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("assets");
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Manufacturer).HasMaxLength(100);
        builder.Property(a => a.Model).HasMaxLength(100);
        builder.Property(a => a.SerialNumber).HasMaxLength(100);
        builder.Property(a => a.Notes).HasMaxLength(2000);
        builder.HasIndex(a => a.SiteId);
        builder.HasIndex(a => a.SerialNumber);
        builder.HasOne<Site>().WithMany().HasForeignKey(a => a.SiteId).OnDelete(DeleteBehavior.Restrict);
    }
}
