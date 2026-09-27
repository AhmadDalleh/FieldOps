using FieldOps.Domain.Common;

namespace FieldOps.Domain.Notifications;

/// <summary>The events of US-NOT-02.</summary>
public enum NotificationType
{
    JobAssigned,
    JobRescheduled,
    JobUnassigned,
    JobCancelled,
    JobOnHold,
    JobCompleted,
    TimeOffRequested,
    TimeOffDecided,
    LowStock,
}

/// <summary>An in-app message for one user, linking to the item it is about (US-NOT-01).</summary>
public sealed class Notification : Entity
{
    private Notification() { }

    public Guid UserId { get; private init; }
    public NotificationType Type { get; private init; }
    public string Title { get; private init; } = null!;
    public string? Body { get; private init; }

    /// <summary>A frontend route such as <c>/office/work-orders/{id}</c>.</summary>
    public string? Link { get; private init; }
    public bool IsRead { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    public static Notification Create(Guid userId, NotificationType type, string title, string? body, string? link, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            Type = type,
            Title = Truncate(title, 200),
            Body = body is null ? null : Truncate(body, 1000),
            Link = link,
            CreatedAt = now,
        };

    public void MarkRead() => IsRead = true;

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
