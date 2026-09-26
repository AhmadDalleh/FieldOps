using FieldOps.Domain.Identity;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issued_token_expires_after_7_days()
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), "hash", Now);

        token.ExpiresAt.ShouldBe(Now.AddDays(7));
        token.IsActive(Now.AddDays(7).AddSeconds(-1)).ShouldBeTrue();
        token.IsActive(Now.AddDays(7)).ShouldBeFalse();
    }

    [Fact]
    public void Revoked_token_is_not_active_and_records_its_replacement()
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), "hash", Now);

        token.Revoke(Now.AddMinutes(1), "next-hash");

        token.IsActive(Now.AddMinutes(2)).ShouldBeFalse();
        token.RevokedAt.ShouldBe(Now.AddMinutes(1));
        token.ReplacedByTokenHash.ShouldBe("next-hash");
    }

    [Fact]
    public void Revoking_twice_keeps_the_first_revocation()
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), "hash", Now);
        token.Revoke(Now.AddMinutes(1), "next-hash");

        token.Revoke(Now.AddMinutes(5));

        token.RevokedAt.ShouldBe(Now.AddMinutes(1));
        token.ReplacedByTokenHash.ShouldBe("next-hash");
    }
}
