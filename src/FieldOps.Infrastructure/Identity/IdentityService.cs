using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using FieldOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Infrastructure.Identity;

public sealed class IdentityService(UserManager<AppUser> users, AppDbContext db) : IIdentityService
{
    public async Task<Result<UserInfo>> CheckPasswordAsync(string email, string password, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null || !user.IsActive) return Errors.Auth.InvalidCredentials;
        if (await users.IsLockedOutAsync(user)) return Errors.Auth.InvalidCredentials;

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return Errors.Auth.InvalidCredentials;
        }

        await users.ResetAccessFailedCountAsync(user);
        return (await FindByIdAsync(user.Id, ct))!;
    }

    public async Task<UserInfo?> FindByIdAsync(Guid userId, CancellationToken ct)
    {
        var row = await Query().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return row?.ToInfo();
    }

    public async Task<IReadOnlyDictionary<Guid, UserInfo>> FindByIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        var rows = await Query().Where(u => ids.Contains(u.Id)).ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => r.ToInfo());
    }

    public async Task<IReadOnlyList<Guid>> ActiveUserIdsInRolesAsync(IReadOnlyCollection<Role> roles, CancellationToken ct)
    {
        var names = roles.Select(r => r.ToString()).ToList();
        return await Query().Where(u => u.IsActive && names.Contains(u.RoleName)).Select(u => u.Id).ToListAsync(ct);
    }

    public async Task<PagedResult<UserInfo>> ListAsync(PageRequest page, CancellationToken ct)
    {
        var query = Query();
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = $"%{page.Search.Trim()}%";
            query = query.Where(u => EF.Functions.ILike(u.FullName, term) || EF.Functions.ILike(u.Email, term));
        }

        query = page.Sort switch
        {
            "email" => query.OrderBy(u => u.Email),
            "-email" => query.OrderByDescending(u => u.Email),
            "role" => query.OrderBy(u => u.RoleName).ThenBy(u => u.FullName),
            "-name" => query.OrderByDescending(u => u.FullName),
            _ => query.OrderBy(u => u.FullName),
        };

        var rows = await query.ToPagedResultAsync(page, ct);
        return new PagedResult<UserInfo>(rows.Items.Select(r => r.ToInfo()).ToList(), rows.Page, rows.PageSize, rows.TotalCount);
    }

    public async Task<Result<Guid>> CreateAsync(
        string fullName, string email, string? phoneNumber, Role role, string password, CancellationToken ct)
    {
        if (await users.FindByEmailAsync(email) is not null) return Errors.User.EmailTaken;

        var user = new AppUser { FullName = fullName, Email = email, UserName = email, PhoneNumber = phoneNumber };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) return ToError(created);

        var added = await users.AddToRoleAsync(user, role.ToString());
        if (!added.Succeeded) return ToError(added);

        return user.Id;
    }

    public async Task<Result> UpdateAsync(
        Guid userId, string fullName, string email, string? phoneNumber, Role role, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return Errors.User.NotFound;

        var owner = await users.FindByEmailAsync(email);
        if (owner is not null && owner.Id != userId) return Errors.User.EmailTaken;

        user.FullName = fullName;
        user.Email = email;
        user.UserName = email;
        user.PhoneNumber = phoneNumber;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded) return ToError(updated);

        var currentRoles = await users.GetRolesAsync(user);
        if (currentRoles.SequenceEqual([role.ToString()])) return Result.Success();

        var removed = await users.RemoveFromRolesAsync(user, currentRoles);
        if (!removed.Succeeded) return ToError(removed);
        var added = await users.AddToRoleAsync(user, role.ToString());
        return added.Succeeded ? Result.Success() : ToError(added);
    }

    public async Task<Result> SetActiveAsync(Guid userId, bool isActive, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return Errors.User.NotFound;

        user.IsActive = isActive;
        var updated = await users.UpdateAsync(user);
        return updated.Succeeded ? Result.Success() : ToError(updated);
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return Errors.User.NotFound;

        var changed = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        return changed.Succeeded ? Result.Success() : ToError(changed);
    }

    private IQueryable<UserRow> Query() =>
        from u in db.Users.AsNoTracking()
        join ur in db.UserRoles on u.Id equals ur.UserId
        join r in db.Roles on ur.RoleId equals r.Id
        select new UserRow
        {
            Id = u.Id,
            Email = u.Email!,
            FullName = u.FullName,
            PhoneNumber = u.PhoneNumber,
            RoleName = r.Name!,
            IsActive = u.IsActive,
        };

    // An object initializer (not a positional record) keeps the projection filterable and sortable in SQL.
    private sealed class UserRow
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = null!;
        public string FullName { get; init; } = null!;
        public string? PhoneNumber { get; init; }
        public string RoleName { get; init; } = null!;
        public bool IsActive { get; init; }

        public UserInfo ToInfo() => new(Id, Email, FullName, PhoneNumber, Enum.Parse<Role>(RoleName), IsActive);
    }

    private static Error ToError(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")) return Errors.User.EmailTaken;

        var details = result.Errors
            .GroupBy(e => e.Code.Contains("Password") ? "password" : "user")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return Error.Validation("User.Invalid", "The user could not be saved.", details);
    }
}
