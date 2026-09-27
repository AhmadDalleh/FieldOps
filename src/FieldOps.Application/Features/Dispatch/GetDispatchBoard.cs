using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Technicians;
using FieldOps.Domain.Technicians;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Dispatch;

public sealed record BoardTechnician(
    Guid Id, string Name, string Color, TimeOnly WorkingHoursStart, TimeOnly WorkingHoursEnd, IReadOnlyList<SkillDto> Skills);

public sealed record BoardJob(
    Guid Id,
    string Number,
    string Title,
    WorkOrderStatus Status,
    WorkOrderPriority Priority,
    WorkOrderType Type,
    string CustomerName,
    string SiteName,
    string SiteAddress,
    double? Latitude,
    double? Longitude,
    Guid? TechnicianId,
    DateTimeOffset? ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    DateTimeOffset? DueBy,
    Guid? RequiredSkillId);

public sealed record BoardTimeOff(Guid Id, Guid TechnicianId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Reason);

/// <param name="Unassigned">New work orders waiting to be scheduled, Urgent first then soonest due (US-DSP-02 AC1).</param>
public sealed record DispatchBoard(
    DateOnly Date,
    IReadOnlyList<BoardTechnician> Technicians,
    IReadOnlyList<BoardJob> Jobs,
    IReadOnlyList<BoardTimeOff> TimeOff,
    IReadOnlyList<BoardJob> Unassigned);

public sealed record GetDispatchBoardQuery(DateOnly? Date = null);

/// <summary>Everything the dispatch board and map need for one Dubai day.</summary>
public sealed class GetDispatchBoardHandler(IAppDbContext db, TechnicianReader technicians, TimeProvider clock)
    : IQueryHandler<GetDispatchBoardQuery, DispatchBoard>
{
    public async Task<DispatchBoard> Handle(GetDispatchBoardQuery query, CancellationToken ct)
    {
        var day = query.Date ?? clock.Today();
        var (from, to) = BusinessCalendar.DayRange(day);

        var rows = (await technicians.ReadAsync(db.Technicians.Where(t => t.IsActive), ct))
            .Select(t => new BoardTechnician(t.Id, t.FullName, t.Color, t.WorkingHoursStart, t.WorkingHoursEnd, t.Skills))
            .ToList();

        var jobs = (await Jobs(db.WorkOrders.Where(w =>
                w.AssignedTechnicianId != null && w.Status != WorkOrderStatus.Cancelled
                && w.ScheduledStart < to && from < w.ScheduledEnd))
            .ToListAsync(ct))
            .OrderBy(j => j.ScheduledStart)
            .ToList();

        var timeOff = await db.TimeOffs.AsNoTracking()
            .Where(t => t.Status == TimeOffStatus.Approved && t.StartsAt < to && from < t.EndsAt)
            .Select(t => new BoardTimeOff(t.Id, t.TechnicianId, t.StartsAt, t.EndsAt, t.Reason))
            .ToListAsync(ct);

        var unassigned = (await Jobs(db.WorkOrders.Where(w => w.Status == WorkOrderStatus.New)).ToListAsync(ct))
            .OrderBy(j => j.Priority switch
            {
                WorkOrderPriority.Urgent => 0,
                WorkOrderPriority.High => 1,
                WorkOrderPriority.Medium => 2,
                _ => 3,
            })
            .ThenBy(j => j.DueBy ?? DateTimeOffset.MaxValue)
            .ThenBy(j => j.Number)
            .ToList();

        return new DispatchBoard(day, rows, jobs, timeOff, unassigned);
    }

    private IQueryable<BoardJob> Jobs(IQueryable<WorkOrder> workOrders) =>
        from w in workOrders.AsNoTracking()
        join c in db.Customers on w.CustomerId equals c.Id
        join s in db.Sites on w.SiteId equals s.Id
        select new BoardJob(
            w.Id, w.Number, w.Title, w.Status, w.Priority, w.Type, c.Name, s.Name, s.AddressLine1 + ", " + s.City,
            s.Latitude, s.Longitude, w.AssignedTechnicianId, w.ScheduledStart, w.ScheduledEnd, w.DueBy, w.RequiredSkillId);
}
