using FieldOps.Domain.Identity;
using FieldOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FieldOps.Infrastructure.Persistence.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("users");
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public static readonly IReadOnlyDictionary<Role, Guid> RoleIds = new Dictionary<Role, Guid>
    {
        [Role.Admin] = new("01925b6e-0000-7000-8000-00000000a001"),
        [Role.Dispatcher] = new("01925b6e-0000-7000-8000-00000000a002"),
        [Role.Technician] = new("01925b6e-0000-7000-8000-00000000a003"),
    };

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.ToTable("roles");
        builder.HasData(RoleIds.Select(r => new IdentityRole<Guid>
        {
            Id = r.Value,
            Name = r.Key.ToString(),
            NormalizedName = r.Key.ToString().ToUpperInvariant(),
            ConcurrencyStamp = r.Value.ToString(),
        }));
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder) => builder.ToTable("user_roles");
}

internal sealed class UserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("user_claims");
}

internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("user_logins");
}

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("user_tokens");
}

internal sealed class RoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder) => builder.ToTable("role_claims");
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(128);
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
