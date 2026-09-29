using FieldOps.Domain.Assets;
using FieldOps.Domain.WorkOrders;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record WorkOrderCustomer(Guid Id, string Code, string Name, string Phone);

public sealed record WorkOrderSite(
    Guid Id, string Name, string AddressLine1, string? AddressLine2, string City, double? Latitude, double? Longitude, string? AccessNotes);

public sealed record WorkOrderAsset(
    Guid Id, string Name, AssetType AssetType, string? Manufacturer, string? Model, string? SerialNumber,
    DateOnly? WarrantyExpiresOn, bool UnderWarranty);

public sealed record WorkOrderTechnician(Guid Id, string Name, string Color);

public sealed record SkillRef(Guid Id, string Name);

public sealed record WorkOrderTaskDto(Guid Id, int SortOrder, string Description, bool IsDone, DateTimeOffset? DoneAt, string? DoneByName);

public sealed record WorkOrderDto(
    Guid Id,
    string Number,
    WorkOrderStatus Status,
    string Title,
    string? Description,
    WorkOrderType Type,
    WorkOrderPriority Priority,
    DateTimeOffset? DueBy,
    bool IsOverdue,
    DateTimeOffset? ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    WorkOrderCustomer Customer,
    WorkOrderSite Site,
    WorkOrderAsset? Asset,
    WorkOrderTechnician? Technician,
    SkillRef? RequiredSkill,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? CompletionNotes,
    string? SignedByName,
    string? CancelReason,
    IReadOnlyList<WorkOrderTaskDto> Tasks,
    IReadOnlyList<WorkOrderAction> AllowedActions,
    bool IsEditable,
    DateTimeOffset CreatedAt,
    uint Version);

public sealed record WorkOrderListItem(
    Guid Id,
    string Number,
    string Title,
    WorkOrderStatus Status,
    WorkOrderType Type,
    WorkOrderPriority Priority,
    string CustomerName,
    string SiteName,
    string? TechnicianName,
    DateTimeOffset? DueBy,
    DateTimeOffset? ScheduledStart,
    bool IsOverdue,
    DateTimeOffset CreatedAt);

public sealed record WorkOrderHistoryItem(
    WorkOrderStatus? FromStatus, WorkOrderStatus ToStatus, string ChangedByName, DateTimeOffset ChangedAt, string? Note,
    double? Latitude, double? Longitude);
