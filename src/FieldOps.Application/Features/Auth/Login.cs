using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FluentValidation;

namespace FieldOps.Application.Features.Auth;

public sealed record LoginCommand(string Email, string Password);

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginHandler(IAppDbContext db, IIdentityService identity, TokenIssuer issuer)
    : ICommandHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var check = await identity.CheckPasswordAsync(cmd.Email.Trim(), cmd.Password, ct);
        if (check.IsFailure) return check.Error;

        var (response, _) = await issuer.IssueAsync(check.Value, ct);
        await db.SaveChangesAsync(ct);
        return response;
    }
}
