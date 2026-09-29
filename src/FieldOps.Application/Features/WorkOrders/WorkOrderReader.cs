using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

/// <summary>Builds work order DTOs, joining in customer, site, asset and people's names.</summary>
public sealed class WorkOrderReader(IAppDbContext db, IIdentityService identity, TimeProvider clock)
{
    public static bool Overdue(WorkOrder w, DateTimeOffset now) => w.DueBy < now && w.IsOpen;

    public async Task<WorkOrderDto> ReadAsync(Guid id, CancellationToken ct)
    {
        var w = await db.WorkOrders.AsNoTracking().Include(x => x.Tasks).SingleAsync(x => x.Id == id, ct);
        var customer = await db.Customers.AsNoTracking()
            .Where(c => c.Id == w.CustomerId).Select(c => new WorkOrderCustomer(c.Id, c.Code, c.Name, c.Phone)).SingleAsync(ct);
        var site = await db.Sites.AsNoTracking()
            .Where(s => s.Id == w.SiteId)
            .Select(s => new WorkOrderSite(s.Id, s.Name, s.AddressLine1, s.AddressLine2, s.City, s.Latitude, s.Longitude, s.AccessNotes))
            .SingleAsync(ct);

        var today = clock.Today();
        var asset = w.AssetId is null
            ? null
            : await db.Assets.AsNoTracking().Where(a => a.Id == w.AssetId)
                .Select(a => new WorkOrderAsset(a.Id, a.Name, a.AssetType, a.Manufacturer, a.Model, a.SerialNumber,
                    a.WarrantyExpiresOn, a.WarrantyExpiresOn != null && today <= a.WarrantyExpiresOn))
                .SingleAsync(ct);

        var technician = (await TechniciansAsync([w.AssignedTechnicianId], ct)).GetValueOrDefault(w.AssignedTechnicianId ?? Guid.Empty);
        var doneBy = await identity.FindByIdsAsync(w.Tasks.Where(t => t.DoneBy != null).Select(t => t.DoneBy!.Value), ct);

        var tasks = w.Tasks
            .OrderBy(t => t.SortOrder)
            .Select(t => new WorkOrderTaskDto(t.Id, t.SortOrder, t.Description, t.IsDone, t.DoneAt,
                t.DoneBy is { } by && doneBy.TryGetValue(by, out var u) ? u.FullName : null))
            .ToList();

        return new WorkOrderDto(
            w.Id, w.Number, w.Status, w.Title, w.Description, w.Type, w.Priority, w.DueBy, Overdue(w, clock.GetUtcNow()),
            w.ScheduledStart, w.ScheduledEnd, customer, site, asset, technician, w.StartedAt, w.CompletedAt,
            w.CompletionNotes, w.SignedByName, w.CancelReason, tasks,
            Enum.GetValues<WorkOrderAction>().Where(a => WorkOrder.IsAllowed(w.Status, a)).ToList(),
            WorkOrder.EditableStatuses.Contains(w.Status), w.CreatedAt, w.Version);
    }

    /// <summary>Technician id → name and color, for the given (possibly null) ids.</summary>
    public async Task<Dictionary<Guid, WorkOrderTechnician>> TechniciansAsync(IEnumerable<Guid?> technicianIds, CancellationToken ct)
    {
        var ids = technicianIds.OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0) return [];

        var technicians = await db.Technicians.AsNoTracking()
            .Where(t => ids.Contains(t.Id)).Select(t => new { t.Id, t.UserId, t.Color }).ToListAsync(ct);
        var users = await identity.FindByIdsAsync(technicians.Select(t => t.UserId), ct);
        return technicians.ToDictionary(
            t => t.Id,
            t => new WorkOrderTechnician(t.Id, users.TryGetValue(t.UserId, out var u) ? u.FullName : "", t.Color));
    }
}
