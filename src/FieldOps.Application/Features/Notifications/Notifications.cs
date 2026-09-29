using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Notifications;

public sealed record NotificationDto(
    Guid Id, NotificationType Type, string Title, string? Body, string? Link, bool IsRead, DateTimeOffset CreatedAt);

public sealed record NotificationList(IReadOnlyList<NotificationDto> Items, int UnreadCount);

public sealed record ListNotificationsQuery(bool UnreadOnly = false, int Take = 30);

public sealed record MarkNotificationReadCommand(Guid Id);

public sealed record MarkAllNotificationsReadCommand;

public static class NotificationErrors
{
    public static readonly Error NotFound = Error.NotFound("Notification.NotFound", "The notification was not found.");
}

internal static class NotificationMapping
{
    public static NotificationDto ToDto(this Notification n) => new(n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt);
}

/// <summary>US-NOT-01: my latest notifications, newest first, with the unread count for the bell.</summary>
public sealed class ListNotificationsHandler(IAppDbContext db, ICurrentUser user) : IQueryHandler<ListNotificationsQuery, NotificationList>
{
    public async Task<NotificationList> Handle(ListNotificationsQuery query, CancellationToken ct)
    {
        var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == user.UserId);
        var unread = await mine.CountAsync(n => !n.IsRead, ct);
        var items = await (query.UnreadOnly ? mine.Where(n => !n.IsRead) : mine)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Take(Math.Clamp(query.Take, 1, 100))
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
        return new NotificationList(items, unread);
    }
}

/// <summary>Someone else's notification reads as not found, so ids reveal nothing.</summary>
public sealed class MarkNotificationReadHandler(IAppDbContext db, ICurrentUser user) : ICommandHandler<MarkNotificationReadCommand, Result>
{
    public async Task<Result> Handle(MarkNotificationReadCommand cmd, CancellationToken ct)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == cmd.Id && n.UserId == user.UserId, ct);
        if (notification is null) return NotificationErrors.NotFound;
        notification.MarkRead();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class MarkAllNotificationsReadHandler(IAppDbContext db, ICurrentUser user) : ICommandHandler<MarkAllNotificationsReadCommand, Result>
{
    public async Task<Result> Handle(MarkAllNotificationsReadCommand cmd, CancellationToken ct)
    {
        await db.Notifications.Where(n => n.UserId == user.UserId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
        return Result.Success();
    }
}
