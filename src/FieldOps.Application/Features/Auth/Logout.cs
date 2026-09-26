using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Auth;

public sealed record LogoutCommand(string RefreshToken);

public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class LogoutHandler(IAppDbContext db, ICurrentUser user, ITokenService tokens, TimeProvider clock)
    : ICommandHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand cmd, CancellationToken ct)
    {
        var hash = tokens.Hash(cmd.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == user.UserId, ct);

        // Logging out with an unknown token is not an error: the client ends up logged out either way.
        if (token is not null)
        {
            token.Revoke(clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}
