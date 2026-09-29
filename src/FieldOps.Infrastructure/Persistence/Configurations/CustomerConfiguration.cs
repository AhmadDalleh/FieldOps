using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Phone).HasMaxLength(30).IsRequired();
        builder.Property(c => c.TaxRegistrationNumber).HasMaxLength(50);
        builder.Property(c => c.BillingAddress).HasMaxLength(500);
        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.HasIndex(c => c.Code).IsUnique();
        builder.HasIndex(c => c.Phone);
        // The trigram index on lower(name) is created in the CustomersAndSites migration (EF cannot model expression indexes).

        builder.HasMany(c => c.Contacts).WithOne().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Contacts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CustomerContactConfiguration : IEntityTypeConfiguration<CustomerContact>
{
    public void Configure(EntityTypeBuilder<CustomerContact> builder)
    {
        builder.ToTable("customer_contacts");
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.JobTitle).HasMaxLength(100);
    }
}

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("sites");
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.AddressLine1).HasMaxLength(200).IsRequired();
        builder.Property(s => s.AddressLine2).HasMaxLength(200);
        builder.Property(s => s.City).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Region).HasMaxLength(100);
        builder.Property(s => s.Country).HasMaxLength(2).IsRequired();
        builder.Property(s => s.AccessNotes).HasMaxLength(2000);
        builder.HasOne<Customer>().WithMany().HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
