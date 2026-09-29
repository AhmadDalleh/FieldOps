using FieldOps.Application.Abstractions;
using FieldOps.Application.Features.Notifications;
using FieldOps.Domain.Technicians;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FieldOps.Infrastructure.Persistence.Interceptors;

/// <summary>
/// After a save succeeds, delivers what it produced: the notifications handlers added (push and email) and a live
/// <c>WorkOrderChanged</c> event for every work order the save touched, so no handler can forget one. Services are resolved
/// lazily because they depend on the DbContext this interceptor belongs to.
/// </summary>
public sealed class NotificationInterceptor(IServiceProvider services) : SaveChangesInterceptor
{
    private readonly List<(Guid Id, Guid? TechnicianBefore)> _touched = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        if (eventData.Context is { } context) await DeliverAsync(context, ct);
        return await base.SavedChangesAsync(eventData, result, ct);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } context) DeliverAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Discard();

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken ct = default)
    {
        Discard();
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        _touched.Clear();
        if (context is null) return;
        foreach (var entry in context.ChangeTracker.Entries<WorkOrder>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            var before = entry.State == EntityState.Added ? null : (Guid?)entry.OriginalValues[nameof(WorkOrder.AssignedTechnicianId)];
            _touched.Add((entry.Entity.Id, before));
        }
    }

    private void Discard()
    {
        _touched.Clear();
        services.GetRequiredService<NotificationOutbox>().Drain();
    }

    private async Task DeliverAsync(DbContext context, CancellationToken ct)
    {
        var notifications = services.GetRequiredService<NotificationOutbox>().Drain();
        var touched = _touched.ToList();
        _touched.Clear();
        if (notifications.Count == 0 && touched.Count == 0) return;

        var changes = new List<WorkOrderChange>();
        if (touched.Count > 0)
        {
            var ids = touched.Select(t => t.Id).ToList();
            var now = await context.Set<WorkOrder>().AsNoTracking().Where(w => ids.Contains(w.Id))
                .Select(w => new { w.Id, w.Status, w.AssignedTechnicianId }).ToListAsync(ct);
            var technicianIds = now.Select(w => w.AssignedTechnicianId).Concat(touched.Select(t => t.TechnicianBefore)).OfType<Guid>().Distinct().ToList();
            var users = await context.Set<Technician>().AsNoTracking().Where(t => technicianIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.UserId, ct);
            foreach (var w in now)
            {
                var before = touched.First(t => t.Id == w.Id).TechnicianBefore;
                var concerned = new[] { w.AssignedTechnicianId, before }.OfType<Guid>().Distinct()
                    .Where(users.ContainsKey).Select(id => users[id]).ToList();
                changes.Add(new WorkOrderChange(w.Id, w.Status, concerned));
            }
        }

        await services.GetRequiredService<NotificationDelivery>().DeliverAsync(notifications, changes, ct);
    }
}
