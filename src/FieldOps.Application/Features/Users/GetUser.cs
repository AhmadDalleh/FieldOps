using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;

namespace FieldOps.Application.Features.Users;

public sealed record GetUserQuery(Guid Id);

public sealed class GetUserHandler(IIdentityService identity) : IQueryHandler<GetUserQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserQuery query, CancellationToken ct)
    {
        var user = await identity.FindByIdAsync(query.Id, ct);
        return user is null ? Errors.User.NotFound : UserDto.From(user);
    }
}
