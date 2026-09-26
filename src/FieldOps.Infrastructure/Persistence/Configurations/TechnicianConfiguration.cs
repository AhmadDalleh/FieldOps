using FieldOps.Domain.Technicians;
using FieldOps.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

internal sealed class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        builder.ToTable("technicians");
        builder.Property(t => t.EmployeeCode).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Phone).HasMaxLength(30);
        builder.Property(t => t.Color).HasMaxLength(7).IsRequired();
        builder.HasIndex(t => t.UserId).IsUnique();
        builder.HasIndex(t => t.EmployeeCode).IsUnique();
        builder.HasOne<AppUser>().WithOne().HasForeignKey<Technician>(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
