using System.Globalization;
using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Notifications;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Notifications;

/// <summary>A notification added in this request, waiting for the save to succeed before it is pushed (and emailed).</summary>
public sealed record PendingNotification(Notification Notification, bool Email);

/// <summary>Holds this request's new notifications until the database save that stores them succeeds.</summary>
public sealed class NotificationOutbox
{
    private readonly List<PendingNotification> _pending = [];

    public void Add(PendingNotification item) => _pending.Add(item);

    public IReadOnlyList<PendingNotification> Drain()
    {
        var items = _pending.ToList();
        _pending.Clear();
        return items;
    }
}

/// <summary>
/// US-NOT-02: turns business events into notifications. Rows are added to the handler's unit of work, so they are saved with
/// the change they describe; nobody is notified of their own action.
/// </summary>
public sealed class Notifier(IAppDbContext db, IIdentityService identity, ICurrentUser user, TimeProvider clock, NotificationOutbox outbox)
{
    private static readonly Role[] Office = [Role.Admin, Role.Dispatcher];
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public async Task JobAssignedAsync(WorkOrder w, Guid technicianId, bool rescheduled, CancellationToken ct)
    {
        var context = await JobContextAsync(w, ct);
        var title = rescheduled ? $"Job rescheduled: {w.Number}" : $"New job {w.Number}: {w.Title}";
        await TechnicianAsync(technicianId, rescheduled ? NotificationType.JobRescheduled : NotificationType.JobAssigned, title,
            $"{When(w)} · {context}", TechLink(w), email: true, ct);
    }

    public async Task JobRemovedAsync(WorkOrder w, Guid technicianId, bool cancelled, string? reason, CancellationToken ct)
    {
        var context = await JobContextAsync(w, ct);
        var title = cancelled ? $"Job cancelled: {w.Number}" : $"Job taken off your schedule: {w.Number}";
        var body = cancelled && !string.IsNullOrWhiteSpace(reason) ? $"{w.Title} · {context}. Reason: {reason}" : $"{w.Title} · {context}";
        await TechnicianAsync(technicianId, cancelled ? NotificationType.JobCancelled : NotificationType.JobUnassigned, title, body,
            null, email: true, ct);
    }

    public async Task JobOnHoldAsync(WorkOrder w, string? note, CancellationToken ct) =>
        await ToRolesAsync(Office, NotificationType.JobOnHold, $"On hold: {w.Number} {w.Title}",
            string.IsNullOrWhiteSpace(note) ? await JobContextAsync(w, ct) : note, OfficeLink(w), ct);

    public async Task JobCompletedAsync(WorkOrder w, CancellationToken ct) =>
        await ToRolesAsync(Office, NotificationType.JobCompleted, $"Completed: {w.Number} {w.Title}",
            $"{await JobContextAsync(w, ct)}. Ready to invoice.", OfficeLink(w), ct);

    public async Task TimeOffRequestedAsync(Guid technicianId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken ct)
    {
        var name = await TechnicianNameAsync(technicianId, ct);
        await ToRolesAsync(Office, NotificationType.TimeOffRequested, $"Time off requested by {name}",
            $"{Day(startsAt)} to {Day(endsAt)}", "/office/time-off", ct);
    }

    public Task TimeOffDecidedAsync(Guid technicianId, bool approved, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken ct) =>
        TechnicianAsync(technicianId, NotificationType.TimeOffDecided, approved ? "Time off approved" : "Time off rejected",
            $"{Day(startsAt)} to {Day(endsAt)}", "/tech/time-off", email: false, ct);

    public Task LowStockAsync(string sku, string name, decimal total, decimal reorderLevel, CancellationToken ct) =>
        ToRolesAsync([Role.Admin], NotificationType.LowStock, $"Low stock: {sku} {name}",
            $"{total:0.##} left in total; reorder level is {reorderLevel:0.##}.", "/office/inventory/stock", ct);

    private async Task TechnicianAsync(
        Guid technicianId, NotificationType type, string title, string? body, string? link, bool email, CancellationToken ct)
    {
        var userId = await db.Technicians.AsNoTracking().Where(t => t.Id == technicianId).Select(t => (Guid?)t.UserId).SingleOrDefaultAsync(ct);
        if (userId is { } id) Add(id, type, title, body, link, email);
    }

    private async Task ToRolesAsync(IReadOnlyCollection<Role> roles, NotificationType type, string title, string? body, string? link,
        CancellationToken ct)
    {
        foreach (var id in await identity.ActiveUserIdsInRolesAsync(roles, ct)) Add(id, type, title, body, link, email: false);
    }

    private void Add(Guid userId, NotificationType type, string title, string? body, string? link, bool email)
    {
        if (user.IsAuthenticated && userId == user.UserId) return;
        var notification = Notification.Create(userId, type, title, body, link, clock.GetUtcNow());
        db.Notifications.Add(notification);
        outbox.Add(new PendingNotification(notification, email));
    }

    private async Task<string> JobContextAsync(WorkOrder w, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().Where(c => c.Id == w.CustomerId).Select(c => c.Name).SingleOrDefaultAsync(ct);
        var site = await db.Sites.AsNoTracking().Where(s => s.Id == w.SiteId).Select(s => new { s.Name, s.City }).SingleOrDefaultAsync(ct);
        return site is null ? customer ?? "" : $"{customer}, {site.Name}, {site.City}";
    }

    private async Task<string> TechnicianNameAsync(Guid technicianId, CancellationToken ct)
    {
        var userId = await db.Technicians.AsNoTracking().Where(t => t.Id == technicianId).Select(t => t.UserId).SingleAsync(ct);
        return (await identity.FindByIdAsync(userId, ct))?.FullName ?? "a technician";
    }

    private static string TechLink(WorkOrder w) => $"/tech/jobs/{w.Id}";

    private static string OfficeLink(WorkOrder w) => $"/office/work-orders/{w.Id}";

    private static string When(WorkOrder w) =>
        w.ScheduledStart is { } start && w.ScheduledEnd is { } end
            ? $"{Local(start).ToString("ddd d MMM, HH:mm", Culture)}–{Local(end).ToString("HH:mm", Culture)}"
            : "Not scheduled";

    private static string Day(DateTimeOffset at) => Local(at).ToString("ddd d MMM", Culture);

    private static DateTime Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, BusinessCalendar.TimeZone).DateTime;
}
