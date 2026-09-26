namespace FieldOps.Application.Abstractions;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(UserInfo user, Guid? technicianId);

    /// <summary>Returns a new random refresh token. Only its hash is stored.</summary>
    string GenerateRefreshToken();

    string Hash(string refreshToken);
}
