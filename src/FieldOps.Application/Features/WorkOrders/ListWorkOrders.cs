using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

/// <param name="From">First Dubai calendar day on which the work order was created.</param>
/// <param name="To">Last Dubai calendar day on which the work order was created.</param>
public sealed record ListWorkOrdersQuery(
    PageRequest Page,
    IReadOnlyList<WorkOrderStatus>? Statuses = null,
    WorkOrderPriority? Priority = null,
    WorkOrderType? Type = null,
    Guid? TechnicianId = null,
    Guid? CustomerId = null,
    Guid? SiteId = null,
    DateOnly? From = null,
    DateOnly? To = null);

public sealed class ListWorkOrdersHandler(IAppDbContext db, WorkOrderReader reader, TimeProvider clock)
    : IQueryHandler<ListWorkOrdersQuery, PagedResult<WorkOrderListItem>>
{
    public async Task<PagedResult<WorkOrderListItem>> Handle(ListWorkOrdersQuery query, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var workOrders = db.WorkOrders.AsNoTracking();

        if (query.Statuses is { Count: > 0 } statuses) workOrders = workOrders.Where(w => statuses.Contains(w.Status));
        if (query.Priority is { } priority) workOrders = workOrders.Where(w => w.Priority == priority);
        if (query.Type is { } type) workOrders = workOrders.Where(w => w.Type == type);
        if (query.TechnicianId is { } technicianId) workOrders = workOrders.Where(w => w.AssignedTechnicianId == technicianId);
        if (query.CustomerId is { } customerId) workOrders = workOrders.Where(w => w.CustomerId == customerId);
        if (query.SiteId is { } siteId) workOrders = workOrders.Where(w => w.SiteId == siteId);
        if (query.From is { } from)
        {
            var start = BusinessCalendar.DayRange(from).From;
            workOrders = workOrders.Where(w => w.CreatedAt >= start);
        }
        if (query.To is { } to)
        {
            var end = BusinessCalendar.DayRange(to).To;
            workOrders = workOrders.Where(w => w.CreatedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(query.Page.Search))
        {
            var pattern = query.Page.Search.ToContainsPattern();
            workOrders = workOrders.Where(w =>
                EF.Functions.Like(w.Number.ToLower(), pattern, QueryableExtensions.LikeEscape) ||
                EF.Functions.Like(w.Title.ToLower(), pattern, QueryableExtensions.LikeEscape));
        }

        // Priorities are stored as text, so rank them explicitly: Urgent first (US-WO-02 AC1).
        var ranked = workOrders.Select(w => new
        {
            WorkOrder = w,
            Rank = w.Priority == WorkOrderPriority.Urgent ? 0
                : w.Priority == WorkOrderPriority.High ? 1
                : w.Priority == WorkOrderPriority.Medium ? 2 : 3,
        });
        var sorted = query.Page.Sort switch
        {
            "number" => ranked.OrderBy(x => x.WorkOrder.Number),
            "-number" => ranked.OrderByDescending(x => x.WorkOrder.Number),
            "createdAt" => ranked.OrderBy(x => x.WorkOrder.CreatedAt),
            "-createdAt" => ranked.OrderByDescending(x => x.WorkOrder.CreatedAt),
            "dueBy" => ranked.OrderBy(x => x.WorkOrder.DueBy == null).ThenBy(x => x.WorkOrder.DueBy),
            _ => ranked.OrderBy(x => x.Rank).ThenBy(x => x.WorkOrder.DueBy == null).ThenBy(x => x.WorkOrder.DueBy)
                .ThenBy(x => x.WorkOrder.Number),
        };

        var closed = WorkOrder.ClosedStatuses;
        var page = await (
                from x in sorted
                join c in db.Customers on x.WorkOrder.CustomerId equals c.Id
                join s in db.Sites on x.WorkOrder.SiteId equals s.Id
                select new
                {
                    x.WorkOrder.Id, x.WorkOrder.Number, x.WorkOrder.Title, x.WorkOrder.Status, x.WorkOrder.Type,
                    x.WorkOrder.Priority, CustomerName = c.Name, SiteName = s.Name, x.WorkOrder.AssignedTechnicianId,
                    x.WorkOrder.DueBy, x.WorkOrder.ScheduledStart, x.WorkOrder.CreatedAt,
                    IsOverdue = x.WorkOrder.DueBy < now && !closed.Contains(x.WorkOrder.Status),
                })
            .ToPagedResultAsync(query.Page, ct);

        var technicians = await reader.TechniciansAsync(page.Items.Select(i => i.AssignedTechnicianId), ct);
        var items = page.Items
            .Select(i => new WorkOrderListItem(
                i.Id, i.Number, i.Title, i.Status, i.Type, i.Priority, i.CustomerName, i.SiteName,
                i.AssignedTechnicianId is { } t && technicians.TryGetValue(t, out var tech) ? tech.Name : null,
                i.DueBy, i.ScheduledStart, i.IsOverdue, i.CreatedAt))
            .ToList();
        return new PagedResult<WorkOrderListItem>(items, page.Page, page.PageSize, page.TotalCount);
    }
}
