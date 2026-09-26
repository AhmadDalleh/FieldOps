using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using FluentValidation;

namespace FieldOps.Application.Features.Users;

public sealed record UpdateUserCommand(Guid Id, string FullName, string Email, string? PhoneNumber, Role Role);

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class UpdateUserHandler(IAppDbContext db, IIdentityService identity, TechnicianProvisioner technicians)
    : ICommandHandler<UpdateUserCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(UpdateUserCommand cmd, CancellationToken ct)
    {
        var existing = await identity.FindByIdAsync(cmd.Id, ct);
        if (existing is null) return Errors.User.NotFound;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var updated = await identity.UpdateAsync(cmd.Id, cmd.FullName.Trim(), cmd.Email.Trim(), cmd.PhoneNumber, cmd.Role, ct);
        if (updated.IsFailure) return updated.Error;

        if (cmd.Role == Role.Technician)
            await technicians.EnsureActiveAsync(cmd.Id, cmd.FullName.Trim(), cmd.PhoneNumber, ct);
        else if (existing.Role == Role.Technician)
            await technicians.DeactivateAsync(cmd.Id, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return UserDto.From((await identity.FindByIdAsync(cmd.Id, ct))!);
    }
}
