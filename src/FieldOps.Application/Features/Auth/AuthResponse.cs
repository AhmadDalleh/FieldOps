using FieldOps.Domain.Identity;

namespace FieldOps.Application.Features.Auth;

public sealed record AuthUserDto(Guid Id, string Email, string FullName, Role Role, Guid? TechnicianId);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthUserDto User);
