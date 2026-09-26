using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Auth;

public sealed record RefreshCommand(string RefreshToken);

public sealed class RefreshValidator : AbstractValidator<RefreshCommand>
{
    public RefreshValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class RefreshHandler(
    IAppDbContext db,
    IIdentityService identity,
    ITokenService tokens,
    TokenIssuer issuer,
    TimeProvider clock)
    : ICommandHandler<RefreshCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RefreshCommand cmd, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = tokens.Hash(cmd.RefreshToken);
        var current = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (current is null) return Errors.Auth.InvalidRefreshToken;

        if (current.IsRevoked)
        {
            // A revoked token being presented again means it may have been stolen: kill every descendant.
            await RevokeDescendantsAsync(current, now, ct);
            await db.SaveChangesAsync(ct);
            return Errors.Auth.InvalidRefreshToken;
        }

        if (!current.IsActive(now)) return Errors.Auth.InvalidRefreshToken;

        var user = await identity.FindByIdAsync(current.UserId, ct);
        if (user is null || !user.IsActive) return Errors.Auth.InvalidRefreshToken;

        var (response, next) = await issuer.IssueAsync(user, ct);
        current.Revoke(now, next.TokenHash);
        await db.SaveChangesAsync(ct);
        return response;
    }

    private async Task RevokeDescendantsAsync(Domain.Identity.RefreshToken token, DateTimeOffset now, CancellationToken ct)
    {
        var nextHash = token.ReplacedByTokenHash;
        while (nextHash is not null)
        {
            var next = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == nextHash, ct);
            if (next is null) return;
            next.Revoke(now);
            nextHash = next.ReplacedByTokenHash;
        }
    }
}
