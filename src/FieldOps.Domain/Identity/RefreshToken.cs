namespace FieldOps.Domain.Identity;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid UserId { get; private init; }
    public string TokenHash { get; private init; } = null!;
    public DateTimeOffset ExpiresAt { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset now) => new()
    {
        UserId = userId,
        TokenHash = tokenHash,
        CreatedAt = now,
        ExpiresAt = now + Lifetime,
    };

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && now < ExpiresAt;

    public void Revoke(DateTimeOffset now, string? replacedByTokenHash = null)
    {
        if (IsRevoked) return;
        RevokedAt = now;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
