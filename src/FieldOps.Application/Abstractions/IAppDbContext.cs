using FieldOps.Domain.Customers;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Inventory;
using FieldOps.Domain.Settings;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FieldOps.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Technician> Technicians { get; }
    DbSet<StockLocation> StockLocations { get; }
    DbSet<AppSettings> AppSettings { get; }
    DbSet<Customer> Customers { get; }
    DbSet<CustomerContact> CustomerContacts { get; }
    DbSet<Site> Sites { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
