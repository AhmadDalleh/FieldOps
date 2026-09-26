using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;

namespace FieldOps.Application.Features.Auth;

public sealed record GetMeQuery;

public sealed class GetMeHandler(IIdentityService identity, ICurrentUser user)
    : IQueryHandler<GetMeQuery, Result<AuthUserDto>>
{
    public async Task<Result<AuthUserDto>> Handle(GetMeQuery query, CancellationToken ct)
    {
        var info = await identity.FindByIdAsync(user.UserId, ct);
        if (info is null) return Errors.User.NotFound;
        return new AuthUserDto(info.Id, info.Email, info.FullName, info.Role, user.TechnicianId);
    }
}
