using FieldOps.Application.Abstractions;
using FieldOps.Domain.Identity;

namespace FieldOps.Application.Features.Users;

public sealed record UserDto(Guid Id, string Email, string FullName, string? PhoneNumber, Role Role, bool IsActive)
{
    public static UserDto From(UserInfo user) =>
        new(user.Id, user.Email, user.FullName, user.PhoneNumber, user.Role, user.IsActive);
}
