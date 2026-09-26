using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Users;

public sealed record SetUserActiveCommand(Guid Id, bool IsActive);

public sealed class SetUserActiveHandler(
    IAppDbContext db,
    IIdentityService identity,
    ICurrentUser currentUser,
    TechnicianProvisioner technicians,
    TimeProvider clock)
    : ICommandHandler<SetUserActiveCommand, Result>
{
    public async Task<Result> Handle(SetUserActiveCommand cmd, CancellationToken ct)
    {
        var user = await identity.FindByIdAsync(cmd.Id, ct);
        if (user is null) return Errors.User.NotFound;
        if (!cmd.IsActive && cmd.Id == currentUser.UserId) return Errors.User.CannotDeactivateSelf;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var result = await identity.SetActiveAsync(cmd.Id, cmd.IsActive, ct);
        if (result.IsFailure) return result;

        if (cmd.IsActive)
        {
            if (user.Role == Role.Technician)
                await technicians.EnsureActiveAsync(user.Id, user.FullName, user.PhoneNumber, ct);
        }
        else
        {
            await technicians.DeactivateAsync(user.Id, ct);
            var now = clock.GetUtcNow();
            var activeTokens = await db.RefreshTokens.Where(t => t.UserId == cmd.Id && t.RevokedAt == null).ToListAsync(ct);
            foreach (var token in activeTokens) token.Revoke(now);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Success();
    }
}
