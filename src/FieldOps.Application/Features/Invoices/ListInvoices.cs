using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Invoices;

public sealed record InvoiceListItem(
    Guid Id,
    string? Number,
    InvoiceStatus Status,
    Guid WorkOrderId,
    string WorkOrderNumber,
    Guid CustomerId,
    string CustomerName,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    decimal Total,
    bool IsOverdue,
    DateTimeOffset CreatedAt);

/// <summary>
/// <paramref name="From"/> and <paramref name="To"/> are Dubai dates matched against the issue date, or the day a draft was created.
/// </summary>
public sealed record ListInvoicesQuery(
    PageRequest Page,
    InvoiceStatus? Status = null,
    Guid? CustomerId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    bool OverdueOnly = false);

/// <summary>US-BIL-07: newest first; overdue means issued and past the due date.</summary>
public sealed class ListInvoicesHandler(IAppDbContext db, TimeProvider clock) : IQueryHandler<ListInvoicesQuery, PagedResult<InvoiceListItem>>
{
    public async Task<PagedResult<InvoiceListItem>> Handle(ListInvoicesQuery query, CancellationToken ct)
    {
        var today = clock.Today();
        var invoices = db.Invoices.AsNoTracking();
        if (query.Status is { } status) invoices = invoices.Where(i => i.Status == status);
        if (query.CustomerId is { } customerId) invoices = invoices.Where(i => i.CustomerId == customerId);
        if (query.OverdueOnly) invoices = invoices.Where(i => i.Status == InvoiceStatus.Issued && i.DueDate < today);
        if (query.From is { } from)
        {
            var start = BusinessCalendar.DayRange(from).From;
            invoices = invoices.Where(i => i.IssueDate != null ? i.IssueDate >= from : i.CreatedAt >= start);
        }
        if (query.To is { } to)
        {
            var end = BusinessCalendar.DayRange(to).To;
            invoices = invoices.Where(i => i.IssueDate != null ? i.IssueDate <= to : i.CreatedAt < end);
        }

        var rows = invoices
            .Join(db.WorkOrders, i => i.WorkOrderId, w => w.Id, (i, w) => new { i, WorkOrderNumber = w.Number })
            .Join(db.Customers, x => x.i.CustomerId, c => c.Id, (x, c) => new { x.i, x.WorkOrderNumber, CustomerName = c.Name });
        if (!string.IsNullOrWhiteSpace(query.Page.Search))
        {
            var pattern = query.Page.Search.ToContainsPattern();
            rows = rows.Where(x => EF.Functions.Like((x.i.Number ?? "").ToLower(), pattern, QueryableExtensions.LikeEscape)
                || EF.Functions.Like(x.WorkOrderNumber.ToLower(), pattern, QueryableExtensions.LikeEscape)
                || EF.Functions.Like(x.CustomerName.ToLower(), pattern, QueryableExtensions.LikeEscape));
        }

        return await rows
            .OrderByDescending(x => x.i.CreatedAt).ThenByDescending(x => x.i.Id)
            .Select(x => new InvoiceListItem(x.i.Id, x.i.Number, x.i.Status, x.i.WorkOrderId, x.WorkOrderNumber, x.i.CustomerId,
                x.CustomerName, x.i.IssueDate, x.i.DueDate, x.i.Total, x.i.Status == InvoiceStatus.Issued && x.i.DueDate < today,
                x.i.CreatedAt))
            .ToPagedResultAsync(query.Page, ct);
    }
}
