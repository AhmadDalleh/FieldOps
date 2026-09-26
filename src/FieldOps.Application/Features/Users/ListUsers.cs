using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;

namespace FieldOps.Application.Features.Users;

public sealed record ListUsersQuery(PageRequest Page);

public sealed class ListUsersHandler(IIdentityService identity) : IQueryHandler<ListUsersQuery, PagedResult<UserDto>>
{
    public async Task<PagedResult<UserDto>> Handle(ListUsersQuery query, CancellationToken ct)
    {
        var page = await identity.ListAsync(query.Page, ct);
        return new PagedResult<UserDto>(page.Items.Select(UserDto.From).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}
