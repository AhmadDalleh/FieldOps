using FieldOps.Domain.Customers;
using FieldOps.Domain.Invoicing;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices", t => t.HasCheckConstraint("ck_invoices_total", "total >= 0"));
        builder.Property(i => i.Number).HasMaxLength(20);
        builder.Property(i => i.VatRate).HasPrecision(5, 2);
        builder.Property(i => i.PaymentReference).HasMaxLength(100);
        builder.Property(i => i.VoidReason).HasMaxLength(500);
        builder.Property(i => i.PdfKey).HasMaxLength(200);
        builder.Property(i => i.Version).IsRowVersion();
        builder.HasIndex(i => i.Number).IsUnique();
        // One live invoice per work order; a voided one makes room for the next (US-BIL-01 AC1, US-BIL-06 AC1).
        builder.HasIndex(i => i.WorkOrderId).IsUnique().HasFilter("status <> 'Void'");
        builder.HasIndex(i => new { i.Status, i.DueDate });
        builder.HasIndex(i => i.CustomerId);
        builder.HasOne<WorkOrder>().WithMany().HasForeignKey(i => i.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(i => i.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("invoice_lines", t => t.HasCheckConstraint("ck_invoice_lines_quantity", "quantity > 0"));
        builder.Property(l => l.Description).HasMaxLength(300).IsRequired();
        builder.Property(l => l.Quantity).HasPrecision(10, 2);
    }
}
