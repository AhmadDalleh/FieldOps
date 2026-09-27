using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Me;

public enum JobDay { Today, Tomorrow }

public sealed record MyJob(
    Guid Id,
    string Number,
    string Title,
    WorkOrderStatus Status,
    WorkOrderPriority Priority,
    WorkOrderType Type,
    DateTimeOffset? ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    string CustomerName,
    string SiteName,
    string SiteAddress,
    string SiteCity);

public sealed record MyJobsQuery(JobDay Day);

/// <summary>US-TAPP-01: the signed-in technician's open jobs for today or tomorrow, by start time.</summary>
public sealed class MyJobsHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock)
    : IQueryHandler<MyJobsQuery, Result<IReadOnlyList<MyJob>>>
{
    public static readonly WorkOrderStatus[] Shown =
    [
        WorkOrderStatus.Scheduled, WorkOrderStatus.Dispatched, WorkOrderStatus.EnRoute, WorkOrderStatus.InProgress, WorkOrderStatus.OnHold,
    ];

    /// <summary>Jobs already under way stay on today's list even if they were scheduled for an earlier day.</summary>
    private static readonly WorkOrderStatus[] UnderWay = [WorkOrderStatus.EnRoute, WorkOrderStatus.InProgress, WorkOrderStatus.OnHold];

    public async Task<Result<IReadOnlyList<MyJob>>> Handle(MyJobsQuery query, CancellationToken ct)
    {
        if (user.TechnicianId is not { } technicianId) return Errors.Forbidden;

        var today = clock.Today();
        var (from, to) = BusinessCalendar.DayRange(query.Day == JobDay.Today ? today : today.AddDays(1));
        var carryOver = query.Day == JobDay.Today;

        var jobs = await db.WorkOrders.AsNoTracking()
            .Where(w => w.AssignedTechnicianId == technicianId && Shown.Contains(w.Status))
            .Where(w => (w.ScheduledStart >= from && w.ScheduledStart < to)
                || (carryOver && UnderWay.Contains(w.Status) && w.ScheduledStart < from))
            .Join(db.Customers, w => w.CustomerId, c => c.Id, (w, c) => new { w, c })
            .Join(db.Sites, x => x.w.SiteId, s => s.Id, (x, s) => new { x.w, x.c, s })
            .OrderBy(x => x.w.ScheduledStart).ThenBy(x => x.w.Number)
            .Select(x => new MyJob(x.w.Id, x.w.Number, x.w.Title, x.w.Status, x.w.Priority, x.w.Type, x.w.ScheduledStart,
                x.w.ScheduledEnd, x.c.Name, x.s.Name, x.s.AddressLine1, x.s.City))
            .ToListAsync(ct);
        return jobs;
    }
}
