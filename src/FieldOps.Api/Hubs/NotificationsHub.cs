using FieldOps.Application.Abstractions;
using FieldOps.Application.Features.Notifications;
using FieldOps.Domain.Identity;
using FieldOps.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FieldOps.Api.Hubs;

/// <summary>
/// The one hub (docs/02-architecture.md): each connection joins <c>user:{id}</c>, and Admins and Dispatchers also join
/// <c>office</c>. The server only sends; clients listen for <c>NotificationCreated</c> and <c>WorkOrderChanged</c>.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";
    public const string OfficeGroup = "office";

    public static string UserGroup(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (Guid.TryParse(user?.FindFirst(ClaimNames.Subject)?.Value, out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        if (Enum.TryParse<Role>(user?.FindFirst(ClaimNames.Role)?.Value, out var role) && role is Role.Admin or Role.Dispatcher)
            await Groups.AddToGroupAsync(Context.ConnectionId, OfficeGroup);
        await base.OnConnectedAsync();
    }
}

public sealed class SignalRNotifier(IHubContext<NotificationsHub> hub) : INotifier
{
    public Task NotificationCreatedAsync(Guid userId, NotificationDto notification, CancellationToken ct) =>
        hub.Clients.Group(NotificationsHub.UserGroup(userId)).SendAsync("NotificationCreated", notification, ct);

    public Task WorkOrderChangedAsync(WorkOrderChange change, CancellationToken ct)
    {
        var groups = change.TechnicianUserIds.Select(NotificationsHub.UserGroup).Append(NotificationsHub.OfficeGroup).ToList();
        return hub.Clients.Groups(groups).SendAsync("WorkOrderChanged", new { change.Id, change.Status }, ct);
    }
}
