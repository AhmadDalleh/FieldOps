using FieldOps.Domain.Assets;
using FieldOps.Domain.Customers;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Inventory;
using FieldOps.Domain.Settings;
using FieldOps.Domain.Technicians;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
    DbSet<Asset> Assets { get; }
    DbSet<Skill> Skills { get; }
    DbSet<TimeOff> TimeOffs { get; }
    DbSet<WorkOrder> WorkOrders { get; }
    DbSet<WorkOrderTask> WorkOrderTasks { get; }
    DbSet<WorkOrderNote> WorkOrderNotes { get; }
    DbSet<WorkOrderStatusHistory> WorkOrderStatusHistory { get; }
    DbSet<ChecklistTemplate> ChecklistTemplates { get; }

    DatabaseFacade Database { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
