using FieldOps.Domain.Assets;
using FieldOps.Domain.Customers;
using FieldOps.Domain.Technicians;
using FieldOps.Domain.WorkOrders;
using FieldOps.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("work_orders");
        builder.Property(w => w.Number).HasMaxLength(20).IsRequired();
        builder.Property(w => w.Title).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(4000);
        builder.Property(w => w.CompletionNotes).HasMaxLength(4000);
        builder.Property(w => w.SignedByName).HasMaxLength(200);
        builder.Property(w => w.CancelReason).HasMaxLength(500);
        builder.Property(w => w.Version).IsRowVersion();

        builder.HasIndex(w => w.Number).IsUnique();
        builder.HasIndex(w => w.Status);
        builder.HasIndex(w => new { w.AssignedTechnicianId, w.ScheduledStart });
        builder.HasIndex(w => new { w.CustomerId, w.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(w => w.SiteId);
        builder.HasIndex(w => w.AssetId);

        builder.HasOne<Customer>().WithMany().HasForeignKey(w => w.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Site>().WithMany().HasForeignKey(w => w.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(w => w.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Skill>().WithMany().HasForeignKey(w => w.RequiredSkillId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Technician>().WithMany().HasForeignKey(w => w.AssignedTechnicianId).OnDelete(DeleteBehavior.Restrict);
        // TODO(P6): foreign key from signature_attachment_id to attachments once that table exists.

        builder.HasMany(w => w.Tasks).WithOne().HasForeignKey(t => t.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(w => w.Tasks).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(w => w.History).WithOne().HasForeignKey(h => h.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(w => w.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class WorkOrderTaskConfiguration : IEntityTypeConfiguration<WorkOrderTask>
{
    public void Configure(EntityTypeBuilder<WorkOrderTask> builder)
    {
        builder.ToTable("work_order_tasks");
        builder.Property(t => t.Description).HasMaxLength(500).IsRequired();
        builder.HasIndex(t => new { t.WorkOrderId, t.SortOrder });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(t => t.DoneBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkOrderStatusHistoryConfiguration : IEntityTypeConfiguration<WorkOrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<WorkOrderStatusHistory> builder)
    {
        builder.ToTable("work_order_status_history");
        builder.Property(h => h.Note).HasMaxLength(2000);
        builder.HasIndex(h => new { h.WorkOrderId, h.ChangedAt });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(h => h.ChangedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkOrderNoteConfiguration : IEntityTypeConfiguration<WorkOrderNote>
{
    public void Configure(EntityTypeBuilder<WorkOrderNote> builder)
    {
        builder.ToTable("work_order_notes");
        builder.Property(n => n.Body).HasMaxLength(4000).IsRequired();
        builder.HasIndex(n => new { n.WorkOrderId, n.CreatedAt });
        builder.HasOne<WorkOrder>().WithMany().HasForeignKey(n => n.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(n => n.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ChecklistTemplateConfiguration : IEntityTypeConfiguration<ChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplate> builder)
    {
        builder.ToTable("checklist_templates");
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.HasMany(t => t.Items).WithOne().HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ChecklistTemplateItemConfiguration : IEntityTypeConfiguration<ChecklistTemplateItem>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplateItem> builder)
    {
        builder.ToTable("checklist_template_items");
        builder.Property(i => i.Description).HasMaxLength(500).IsRequired();
    }
}
