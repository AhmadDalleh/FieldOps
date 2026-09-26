namespace FieldOps.Application.Common;

public sealed record PageRequest(int Page = 1, int PageSize = 20, string? Search = null, string? Sort = null)
{
    public const int MaxPageSize = 100;

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}
