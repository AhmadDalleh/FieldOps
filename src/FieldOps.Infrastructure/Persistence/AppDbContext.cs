using FieldOps.Application.Abstractions;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Inventory;
using FieldOps.Domain.Settings;
using FieldOps.Domain.Technicians;
using FieldOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<StockLocation> StockLocations => Set<StockLocation>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(18, 2);
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50);
    }
}
