using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FluentValidation;

namespace FieldOps.Application.Features.Auth;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).Password();
    }
}

public sealed class ChangePasswordHandler(IIdentityService identity, ICurrentUser user)
    : ICommandHandler<ChangePasswordCommand, Result>
{
    public Task<Result> Handle(ChangePasswordCommand cmd, CancellationToken ct) =>
        identity.ChangePasswordAsync(user.UserId, cmd.CurrentPassword, cmd.NewPassword, ct);
}
