using FieldOps.Application.Abstractions;
using FieldOps.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Auth;

/// <summary>Creates an access/refresh token pair and stages the refresh token for saving.</summary>
public sealed class TokenIssuer(IAppDbContext db, ITokenService tokens, TimeProvider clock)
{
    public async Task<(AuthResponse Response, RefreshToken Stored)> IssueAsync(UserInfo user, CancellationToken ct)
    {
        var technicianId = user.Role == Role.Technician
            ? await db.Technicians.Where(t => t.UserId == user.Id).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct)
            : null;

        var access = tokens.CreateAccessToken(user, technicianId);
        var rawRefresh = tokens.GenerateRefreshToken();
        var stored = RefreshToken.Issue(user.Id, tokens.Hash(rawRefresh), clock.GetUtcNow());
        db.RefreshTokens.Add(stored);

        var response = new AuthResponse(
            access.Token,
            access.ExpiresAt,
            rawRefresh,
            stored.ExpiresAt,
            new AuthUserDto(user.Id, user.Email, user.FullName, user.Role, technicianId));
        return (response, stored);
    }
}
