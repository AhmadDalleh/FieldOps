using FieldOps.Domain.Common;

namespace FieldOps.Domain.WorkOrders;

public enum AttachmentKind { Photo, Document, Signature }

/// <summary>A file on a work order. The bytes live in file storage under <see cref="StorageKey"/>.</summary>
public sealed class Attachment : Entity
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;
    public static readonly string[] ImageContentTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Photos can be taken from the moment the technician sets off until the job is done (US-TAPP-06 AC2).</summary>
    public static readonly WorkOrderStatus[] PhotoStatuses = [WorkOrderStatus.EnRoute, WorkOrderStatus.InProgress, WorkOrderStatus.OnHold];

    private Attachment() { }

    public Guid WorkOrderId { get; private init; }
    public AttachmentKind Kind { get; private init; }
    public string FileName { get; private init; } = null!;
    public string ContentType { get; private init; } = null!;
    public long SizeBytes { get; private init; }
    public string StorageKey { get; private init; } = null!;
    public Guid UploadedBy { get; private init; }
    public DateTimeOffset UploadedAt { get; private init; }

    public static Result<Attachment> Create(
        WorkOrder workOrder, AttachmentKind kind, string fileName, string contentType, long sizeBytes, Guid uploadedBy, DateTimeOffset now)
    {
        if (sizeBytes <= 0 || sizeBytes > MaxSizeBytes) return AttachmentErrors.TooLarge;
        if (kind != AttachmentKind.Document && !ImageContentTypes.Contains(contentType)) return AttachmentErrors.UnsupportedType;

        var allowed = kind switch
        {
            AttachmentKind.Photo => PhotoStatuses.Contains(workOrder.Status),
            AttachmentKind.Signature => workOrder.Status == WorkOrderStatus.InProgress,
            _ => workOrder.Status != WorkOrderStatus.Cancelled,
        };
        if (!allowed) return AttachmentErrors.NotAllowedNow(kind, workOrder.Status);

        var id = Guid.CreateVersion7();
        var extension = Path.GetExtension(fileName);
        return new Attachment
        {
            Id = id,
            WorkOrderId = workOrder.Id,
            Kind = kind,
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = $"work-orders/{workOrder.Id}/{id}{extension}",
            UploadedBy = uploadedBy,
            UploadedAt = now,
        };
    }

    /// <summary>The uploader may delete their own file until the job is completed (US-TAPP-06 AC2).</summary>
    public Result CanDelete(WorkOrder workOrder, Guid userId)
    {
        if (UploadedBy != userId) return AttachmentErrors.NotYours;
        if (!workOrder.IsOpen || workOrder.SignatureAttachmentId == Id) return AttachmentErrors.Locked;
        return Result.Success();
    }
}

public static class AttachmentErrors
{
    public static readonly Error NotFound = Error.NotFound("Attachment.NotFound", "The file was not found.");
    public static readonly Error TooLarge = Error.Validation("Attachment.TooLarge", "Files must be between 1 byte and 10 MB.");
    public static readonly Error UnsupportedType = Error.Validation("Attachment.UnsupportedType",
        "Photos and signatures must be JPEG, PNG or WebP images.");
    public static readonly Error NotYours = Error.Forbidden("Attachment.NotYours", "Only the person who uploaded a file can delete it.");
    public static readonly Error Locked = Error.Conflict("Attachment.Locked", "Files cannot be deleted once the job is completed.");
    public static readonly Error InvalidSignature = Error.Validation("Attachment.InvalidSignature",
        "The signature must be a signature image uploaded to this work order.");

    public static Error NotAllowedNow(AttachmentKind kind, WorkOrderStatus status) =>
        Error.Conflict("Attachment.NotAllowedNow", $"A {kind.ToString().ToLowerInvariant()} cannot be added while the job is {status}.");
}
