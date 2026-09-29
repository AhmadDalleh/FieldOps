using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Technicians;
using FieldOps.Domain.Technicians;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Dashboard;

public sealed record StatusCount(WorkOrderStatus Status, int Count);

/// <summary>Busy: on the way to or working on a job. Off: on approved time off right now. Free: everyone else.</summary>
public enum TechnicianState { Busy, Free, Off }

public sealed record DashboardTechnician(
    Guid Id, string Name, string Color, TechnicianState State, Guid? CurrentJobId, string? CurrentJobNumber, int JobsToday);

public sealed record DashboardJob(
    Guid Id, string Number, string Title, WorkOrderPriority Priority, string CustomerName, DateTimeOffset? DueBy, bool IsOverdue,
    DateTimeOffset CreatedAt);

/// <param name="OpenByStatus">Every open status, zeros included, in state-machine order.</param>
/// <param name="Unassigned">New work orders nobody is scheduled for.</param>
/// <param name="Overdue">Open work orders past their due time.</param>
/// <param name="UrgentUnassigned">Urgent New work orders, soonest due first.</param>
public sealed record DashboardDto(
    DateOnly Date,
    IReadOnlyList<StatusCount> OpenByStatus,
    int Unassigned,
    int Overdue,
    int CompletedToday,
    int TechniciansBusy,
    int TechniciansFree,
    int TechniciansOff,
    IReadOnlyList<DashboardTechnician> Technicians,
    IReadOnlyList<DashboardJob> UrgentUnassigned);

public sealed record GetDashboardQuery;

/// <summary>US-DSH-01: the office's view of today (Dubai day), as of now.</summary>
public sealed class GetDashboardHandler(IAppDbContext db, TechnicianReader technicians, TimeProvider clock)
    : IQueryHandler<GetDashboardQuery, DashboardDto>
{
    private static readonly WorkOrderStatus[] OpenStatuses =
        [.. Enum.GetValues<WorkOrderStatus>().Where(s => !WorkOrder.ClosedStatuses.Contains(s))];

    private static readonly WorkOrderStatus[] OnJob = [WorkOrderStatus.EnRoute, WorkOrderStatus.InProgress];

    public const int UrgentListSize = 10;

    public async Task<DashboardDto> Handle(GetDashboardQuery query, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = clock.Today();
        var (from, to) = BusinessCalendar.DayRange(today);
        var workOrders = db.WorkOrders.AsNoTracking();

        var counts = await workOrders
            .Where(w => OpenStatuses.Contains(w.Status))
            .GroupBy(w => w.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        var overdue = await workOrders.CountAsync(w => OpenStatuses.Contains(w.Status) && w.DueBy < now, ct);
        var completedToday = await workOrders.CountAsync(w => w.CompletedAt >= from && w.CompletedAt < to
            && w.Status != WorkOrderStatus.Cancelled, ct);

        var current = await workOrders
            .Where(w => OnJob.Contains(w.Status) && w.AssignedTechnicianId != null)
            .OrderBy(w => w.ScheduledStart)
            .Select(w => new { TechnicianId = w.AssignedTechnicianId!.Value, w.Id, w.Number })
            .ToListAsync(ct);
        var currentJob = current.GroupBy(j => j.TechnicianId).ToDictionary(g => g.Key, g => g.First());
        var jobsToday = await workOrders
            .Where(w => w.AssignedTechnicianId != null && w.Status != WorkOrderStatus.Cancelled
                && w.ScheduledStart >= from && w.ScheduledStart < to)
            .GroupBy(w => w.AssignedTechnicianId!.Value)
            .Select(g => new { TechnicianId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TechnicianId, x => x.Count, ct);
        var off = (await db.TimeOffs.AsNoTracking()
                .Where(t => t.Status == TimeOffStatus.Approved && t.StartsAt <= now && now < t.EndsAt)
                .Select(t => t.TechnicianId)
                .ToListAsync(ct))
            .ToHashSet();

        var team = (await technicians.ReadAsync(db.Technicians.Where(t => t.IsActive), ct))
            .Select(t =>
            {
                var job = currentJob.GetValueOrDefault(t.Id);
                var state = job is not null ? TechnicianState.Busy : off.Contains(t.Id) ? TechnicianState.Off : TechnicianState.Free;
                return new DashboardTechnician(t.Id, t.FullName, t.Color, state, job?.Id, job?.Number, jobsToday.GetValueOrDefault(t.Id));
            })
            .OrderBy(t => t.State)
            .ThenBy(t => t.Name)
            .ToList();

        var urgent = await (
                from w in workOrders
                join c in db.Customers on w.CustomerId equals c.Id
                where w.Status == WorkOrderStatus.New && w.Priority == WorkOrderPriority.Urgent
                orderby w.DueBy == null, w.DueBy, w.CreatedAt
                select new DashboardJob(w.Id, w.Number, w.Title, w.Priority, c.Name, w.DueBy, w.DueBy < now, w.CreatedAt))
            .Take(UrgentListSize)
            .ToListAsync(ct);

        return new DashboardDto(
            today,
            [.. OpenStatuses.Select(s => new StatusCount(s, counts.GetValueOrDefault(s)))],
            counts.GetValueOrDefault(WorkOrderStatus.New),
            overdue,
            completedToday,
            team.Count(t => t.State == TechnicianState.Busy),
            team.Count(t => t.State == TechnicianState.Free),
            team.Count(t => t.State == TechnicianState.Off),
            team,
            urgent);
    }
}
