using FieldOps.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FieldOps.Application.Features.Notifications;

/// <summary>Where emails point people: the address the web app is served from.</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "App";

    public string BaseUrl { get; set; } = "http://localhost:4200";
}

/// <summary>
/// Runs after a successful save: pushes new notifications and work order changes to browsers and emails technicians.
/// US-NOT-02 AC1: a failed push or email is logged and never fails the operation that caused it.
/// </summary>
public sealed class NotificationDelivery(
    INotifier notifier, IEmailSender email, IIdentityService identity, NotificationOptions options, ILogger<NotificationDelivery> logger)
{
    public async Task DeliverAsync(IReadOnlyList<PendingNotification> notifications, IReadOnlyList<WorkOrderChange> changes, CancellationToken ct)
    {
        foreach (var change in changes)
            await SafelyAsync(() => notifier.WorkOrderChangedAsync(change, ct), "push work order", change.Id);

        foreach (var (n, _) in notifications)
            await SafelyAsync(() => notifier.NotificationCreatedAsync(n.UserId, n.ToDto(), ct), "push notification", n.Id);

        var toEmail = notifications.Where(p => p.Email).Select(p => p.Notification).ToList();
        if (toEmail.Count == 0) return;
        var users = await identity.FindByIdsAsync(toEmail.Select(n => n.UserId), ct);
        foreach (var n in toEmail)
        {
            if (!users.TryGetValue(n.UserId, out var to) || !to.IsActive) continue;
            var link = n.Link is null ? options.BaseUrl : options.BaseUrl.TrimEnd('/') + n.Link;
            var body = $"Hello {to.FullName},\n\n{n.Title}\n{n.Body}\n\nOpen FieldOps: {link}\n";
            await SafelyAsync(() => email.SendAsync(new EmailMessage(to.Email, n.Title, body), ct), "email notification", n.Id);
        }
    }

    private async Task SafelyAsync(Func<Task> action, string what, Guid id)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not {Action} {Id}", what, id);
        }
    }
}
