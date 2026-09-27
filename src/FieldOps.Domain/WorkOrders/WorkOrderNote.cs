using FieldOps.Domain.Common;

namespace FieldOps.Domain.WorkOrders;

/// <summary>An append-only note; its author may correct it within <see cref="EditWindow"/>.</summary>
public sealed class WorkOrderNote : AuditableEntity
{
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(15);

    private WorkOrderNote() { }

    public Guid WorkOrderId { get; private init; }
    public Guid AuthorId { get; private init; }
    public string Body { get; private set; } = null!;
    /// <summary>Internal notes are left off the customer-facing PDF.</summary>
    public bool IsInternal { get; private set; }

    public static WorkOrderNote Create(Guid workOrderId, Guid authorId, string body, bool isInternal) => new()
    {
        WorkOrderId = workOrderId,
        AuthorId = authorId,
        Body = body,
        IsInternal = isInternal,
    };

    public Result Edit(Guid userId, string body, bool isInternal, DateTimeOffset now)
    {
        if (userId != AuthorId) return WorkOrderErrors.NoteNotYours;
        if (now - CreatedAt > EditWindow) return WorkOrderErrors.NoteEditWindowClosed;

        Body = body;
        IsInternal = isInternal;
        return Result.Success();
    }
}
