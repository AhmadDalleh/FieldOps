using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;

namespace FieldOps.Application.Abstractions;

public sealed record UserInfo(Guid Id, string Email, string FullName, string? PhoneNumber, Role Role, bool IsActive);

/// <summary>ASP.NET Core Identity lives in Infrastructure; handlers reach users through this port.</summary>
public interface IIdentityService
{
    /// <summary>Checks credentials and applies lockout. Every failure returns the same generic error.</summary>
    Task<Result<UserInfo>> CheckPasswordAsync(string email, string password, CancellationToken ct);

    Task<UserInfo?> FindByIdAsync(Guid userId, CancellationToken ct);

    Task<PagedResult<UserInfo>> ListAsync(PageRequest page, CancellationToken ct);

    Task<Result<Guid>> CreateAsync(string fullName, string email, string? phoneNumber, Role role, string password, CancellationToken ct);

    Task<Result> UpdateAsync(Guid userId, string fullName, string email, string? phoneNumber, Role role, CancellationToken ct);

    Task<Result> SetActiveAsync(Guid userId, bool isActive, CancellationToken ct);

    Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);
}
