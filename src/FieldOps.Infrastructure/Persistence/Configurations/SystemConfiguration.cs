using FieldOps.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

internal sealed class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.ToTable("app_settings");
        builder.Property(s => s.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.CompanyAddress).HasMaxLength(500);
        builder.Property(s => s.Trn).HasMaxLength(50);
        builder.Property(s => s.LogoKey).HasMaxLength(200);
        builder.Property(s => s.VatRate).HasPrecision(5, 2);
        builder.Property(s => s.Currency).HasMaxLength(3).IsRequired();

        var defaults = Domain.Settings.AppSettings.CreateDefault();
        builder.HasData(new
        {
            defaults.Id,
            defaults.CompanyName,
            defaults.VatRate,
            defaults.LaborRatePerHour,
            defaults.InvoiceDueDays,
            defaults.Currency,
            CreatedAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        });
    }
}

internal sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("number_sequences");
        builder.HasKey(s => s.Name);
        builder.Property(s => s.Name).HasMaxLength(50);
    }
}
