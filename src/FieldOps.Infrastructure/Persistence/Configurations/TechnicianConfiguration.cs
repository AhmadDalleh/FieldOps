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
        builder.HasMany(t => t.Skills).WithOne().HasForeignKey(s => s.TechnicianId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Skills).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TechnicianSkillConfiguration : IEntityTypeConfiguration<TechnicianSkill>
{
    public void Configure(EntityTypeBuilder<TechnicianSkill> builder)
    {
        builder.ToTable("technician_skills");
        builder.HasKey(s => new { s.TechnicianId, s.SkillId });
        builder.HasOne<Skill>().WithMany().HasForeignKey(s => s.SkillId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        // Names are unique ignoring case: the unique index on lower(name) is created in the Technicians migration.
    }
}

internal sealed class TimeOffConfiguration : IEntityTypeConfiguration<TimeOff>
{
    public void Configure(EntityTypeBuilder<TimeOff> builder)
    {
        builder.ToTable("time_off");
        builder.Property(t => t.Reason).HasMaxLength(500);
        builder.HasIndex(t => new { t.TechnicianId, t.StartsAt, t.EndsAt });
        builder.HasOne<Technician>().WithMany().HasForeignKey(t => t.TechnicianId).OnDelete(DeleteBehavior.Restrict);
    }
}
