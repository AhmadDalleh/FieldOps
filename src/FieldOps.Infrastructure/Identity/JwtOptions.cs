namespace FieldOps.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "FieldOps";
    public string Audience { get; set; } = "FieldOps";
    public string Key { get; set; } = null!;
    public int AccessTokenMinutes { get; set; } = 15;
}
