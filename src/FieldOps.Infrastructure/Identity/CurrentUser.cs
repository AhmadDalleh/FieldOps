using FieldOps.Application.Abstractions;
using FieldOps.Domain.Identity;
using Microsoft.AspNetCore.Http;

namespace FieldOps.Infrastructure.Identity;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private string? Claim(string type) => accessor.HttpContext?.User.FindFirst(type)?.Value;

    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.TryParse(Claim(ClaimNames.Subject), out var id)
        ? id
        : throw new InvalidOperationException("There is no authenticated user.");

    public Role? Role => Enum.TryParse<Role>(Claim(ClaimNames.Role), out var role) ? role : null;

    public Guid? TechnicianId => Guid.TryParse(Claim(ClaimNames.TechnicianId), out var id) ? id : null;
}
