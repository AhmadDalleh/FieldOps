using FieldOps.Application.Features.Notifications;
using FieldOps.Domain.WorkOrders;

namespace FieldOps.Application.Abstractions;

/// <summary>A work order that changed, for live screens: the office and the technicians it concerns.</summary>
public sealed record WorkOrderChange(Guid Id, WorkOrderStatus Status, IReadOnlyCollection<Guid> TechnicianUserIds);

/// <summary>Pushes live events to browsers (SignalR in the API), so handlers never see the transport.</summary>
public interface INotifier
{
    Task NotificationCreatedAsync(Guid userId, NotificationDto notification, CancellationToken ct);

    Task WorkOrderChangedAsync(WorkOrderChange change, CancellationToken ct);
}
