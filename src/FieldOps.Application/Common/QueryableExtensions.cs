using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Common;

public static class QueryableExtensions
{
    public const string LikeEscape = "\\";

    /// <summary>Lower-cases and escapes a search term for a case-insensitive, partial LIKE match.</summary>
    public static string ToContainsPattern(this string search)
    {
        var escaped = search.Trim().ToLowerInvariant()
            .Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");
        return $"%{escaped}%";
    }

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
