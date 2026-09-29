using FieldOps.Domain.WorkOrders;
using FluentValidation;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record CreateWorkOrderInput(
    Guid CustomerId,
    Guid SiteId,
    Guid? AssetId,
    string Title,
    string? Description,
    WorkOrderType Type,
    WorkOrderPriority Priority,
    DateTimeOffset? DueBy,
    Guid? RequiredSkillId = null);

/// <param name="Version">The version the client loaded; a different current version means someone else saved first.</param>
public sealed record UpdateWorkOrderInput(
    string Title,
    string? Description,
    WorkOrderType Type,
    WorkOrderPriority Priority,
    DateTimeOffset? DueBy,
    Guid? AssetId,
    uint Version,
    Guid? RequiredSkillId = null);

internal static class WorkOrderInputRules
{
    public static WorkOrderDetails ToDetails(string title, string? description, WorkOrderType type,
        WorkOrderPriority priority, DateTimeOffset? dueBy, Guid? assetId, Guid? requiredSkillId) =>
        new(title.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim(), type, priority, dueBy, assetId,
            requiredSkillId);
}

public sealed class CreateWorkOrderInputValidator : AbstractValidator<CreateWorkOrderInput>
{
    public CreateWorkOrderInputValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class UpdateWorkOrderInputValidator : AbstractValidator<UpdateWorkOrderInput>
{
    public UpdateWorkOrderInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
    }
}
