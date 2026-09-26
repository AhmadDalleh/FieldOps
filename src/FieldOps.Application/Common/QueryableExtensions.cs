using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Common;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageRequest page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page.SafePage - 1) * page.SafePageSize)
            .Take(page.SafePageSize)
            .ToListAsync(ct);
        return new PagedResult<T>(items, page.SafePage, page.SafePageSize, total);
    }
}
