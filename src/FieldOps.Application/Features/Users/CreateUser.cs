using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using FluentValidation;

namespace FieldOps.Application.Features.Users;

public sealed record CreateUserCommand(string FullName, string Email, string? PhoneNumber, Role Role, string TemporaryPassword);

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.TemporaryPassword).Password();
    }
}

public sealed class CreateUserHandler(IAppDbContext db, IIdentityService identity, TechnicianProvisioner technicians)
    : ICommandHandler<CreateUserCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(CreateUserCommand cmd, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var created = await identity.CreateAsync(cmd.FullName.Trim(), cmd.Email.Trim(), cmd.PhoneNumber, cmd.Role, cmd.TemporaryPassword, ct);
        if (created.IsFailure) return created.Error;

        if (cmd.Role == Role.Technician)
            await technicians.EnsureActiveAsync(created.Value, cmd.FullName.Trim(), cmd.PhoneNumber, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var user = await identity.FindByIdAsync(created.Value, ct);
        return UserDto.From(user!);
    }
}
