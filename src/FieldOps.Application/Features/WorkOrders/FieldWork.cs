using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

/// <summary>Where the technician was when they tapped the button, if the browser shared it.</summary>
public sealed record LocationInput(double? Lat, double? Lng);

public sealed record CompleteInput(string? CompletionNotes, string? SignedByName, Guid? SignatureAttachmentId, string? SkippedTasksReason);

public sealed class LocationInputValidator : AbstractValidator<LocationInput>
{
    public LocationInputValidator()
    {
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90);
        RuleFor(x => x.Lng).InclusiveBetween(-180, 180);
        RuleFor(x => x.Lng).NotNull().When(x => x.Lat is not null).WithMessage("Send both latitude and longitude, or neither.");
        RuleFor(x => x.Lat).NotNull().When(x => x.Lng is not null).WithMessage("Send both latitude and longitude, or neither.");
    }
}

public sealed class CompleteInputValidator : AbstractValidator<CompleteInput>
{
    public CompleteInputValidator()
    {
        RuleFor(x => x.CompletionNotes).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.SignedByName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SignatureAttachmentId).NotEmpty();
        RuleFor(x => x.SkippedTasksReason).MaximumLength(1000);
    }
}

public sealed record EnRouteCommand(Guid Id, LocationInput Input);

public sealed record StartWorkOrderCommand(Guid Id, LocationInput Input);

public sealed record CompleteWorkOrderCommand(Guid Id, CompleteInput Input);

/// <summary>US-TAPP-03: only the assigned technician sets off.</summary>
public sealed class EnRouteHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader)
    : ICommandHandler<EnRouteCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(EnRouteCommand cmd, CancellationToken ct) =>
        FieldWork.RunAsync(db, user, reader, cmd.Id,
            w => w.EnRoute(user.UserId, clock.GetUtcNow(), cmd.Input.Lat, cmd.Input.Lng), ct);
}

/// <summary>US-TAPP-04: only the assigned technician starts the job.</summary>
public sealed class StartWorkOrderHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader)
    : ICommandHandler<StartWorkOrderCommand, Result<WorkOrderDto>>
{
    public Task<Result<WorkOrderDto>> Handle(StartWorkOrderCommand cmd, CancellationToken ct) =>
        FieldWork.RunAsync(db, user, reader, cmd.Id,
            w => w.Start(user.UserId, clock.GetUtcNow(), cmd.Input.Lat, cmd.Input.Lng), ct);
}

/// <summary>US-TAPP-08: the assigned technician completes the job with the customer's signature.</summary>
public sealed class CompleteWorkOrderHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, WorkOrderReader reader)
    : ICommandHandler<CompleteWorkOrderCommand, Result<WorkOrderDto>>
{
    public async Task<Result<WorkOrderDto>> Handle(CompleteWorkOrderCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        var signatureIsValid = await db.Attachments.AnyAsync(a =>
            a.Id == input.SignatureAttachmentId && a.WorkOrderId == cmd.Id && a.Kind == AttachmentKind.Signature, ct);

        // TODO(P9): notify Office that the job is completed (US-TAPP-08 AC3).
        return await FieldWork.RunAsync(db, user, reader, cmd.Id, w => signatureIsValid
            ? w.Complete(input.CompletionNotes, input.SignedByName, input.SignatureAttachmentId, user.UserId, clock.GetUtcNow(),
                input.SkippedTasksReason)
            : AttachmentErrors.InvalidSignature, ct);
    }
}

internal static class FieldWork
{
    /// <summary>Runs a status change that only the job's assigned technician may make ("T*" without Office).</summary>
    public static Task<Result<WorkOrderDto>> RunAsync(
        IAppDbContext db, ICurrentUser user, WorkOrderReader reader, Guid id, Func<WorkOrder, Result> change, CancellationToken ct) =>
        user.TechnicianId is null
            ? Task.FromResult<Result<WorkOrderDto>>(Errors.Forbidden)
            : StatusChange.RunAsync(db, user, reader, id, change, ct);
}
