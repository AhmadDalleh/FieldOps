using System.Collections.Concurrent;
using FieldOps.Application.Abstractions;
using FieldOps.Application.Features.Notifications;

namespace FieldOps.Application.Tests.Infrastructure;

/// <summary>Stands in for SignalR and SMTP, recording what would have been pushed and emailed.</summary>
public sealed class RecordingNotifier : INotifier, IEmailSender
{
    public ConcurrentQueue<(Guid UserId, NotificationDto Notification)> Pushed { get; } = new();
    public ConcurrentQueue<WorkOrderChange> Changes { get; } = new();
    public ConcurrentQueue<EmailMessage> Emails { get; } = new();

    /// <summary>When set, every email throws, as a down mail server would.</summary>
    public bool FailEmails { get; set; }

    public void Clear()
    {
        Pushed.Clear();
        Changes.Clear();
        Emails.Clear();
    }

    public Task NotificationCreatedAsync(Guid userId, NotificationDto notification, CancellationToken ct)
    {
        Pushed.Enqueue((userId, notification));
        return Task.CompletedTask;
    }

    public Task WorkOrderChangedAsync(WorkOrderChange change, CancellationToken ct)
    {
        Changes.Enqueue(change);
        return Task.CompletedTask;
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (FailEmails) throw new InvalidOperationException("SMTP is down.");
        Emails.Enqueue(message);
        return Task.CompletedTask;
    }
}
