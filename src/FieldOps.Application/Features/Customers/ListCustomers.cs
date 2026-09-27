using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Customers;

public sealed record ListCustomersQuery(PageRequest Page, bool IncludeInactive = false);

public sealed class ListCustomersHandler(IAppDbContext db) : IQueryHandler<ListCustomersQuery, PagedResult<CustomerListItem>>
{
    public Task<PagedResult<CustomerListItem>> Handle(ListCustomersQuery query, CancellationToken ct)
    {
        var customers = db.Customers.AsNoTracking();
        if (!query.IncludeInactive) customers = customers.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Page.Search))
        {
            var pattern = query.Page.Search.ToContainsPattern();
            customers = customers.Where(c =>
                EF.Functions.Like(c.Name.ToLower(), pattern, QueryableExtensions.LikeEscape) ||
                EF.Functions.Like(c.Code.ToLower(), pattern, QueryableExtensions.LikeEscape) ||
                EF.Functions.Like(c.Phone, pattern, QueryableExtensions.LikeEscape) ||
                (c.Email != null && EF.Functions.Like(c.Email.ToLower(), pattern, QueryableExtensions.LikeEscape)));
        }

        customers = query.Page.Sort switch
        {
            "-name" => customers.OrderByDescending(c => c.Name),
            "code" => customers.OrderBy(c => c.Code),
            "-code" => customers.OrderByDescending(c => c.Code),
            "createdAt" => customers.OrderBy(c => c.CreatedAt),
            "-createdAt" => customers.OrderByDescending(c => c.CreatedAt),
            _ => customers.OrderBy(c => c.Name).ThenBy(c => c.Code),
        };

        return customers
            .Select(c => new CustomerListItem(c.Id, c.Code, c.Name, c.Type, c.Phone, c.Email, c.IsActive))
            .ToPagedResultAsync(query.Page, ct);
    }
}
