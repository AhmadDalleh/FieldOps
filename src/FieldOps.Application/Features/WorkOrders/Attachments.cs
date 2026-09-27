using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record AttachmentDto(
    Guid Id, AttachmentKind Kind, string FileName, string ContentType, long SizeBytes, string UploadedByName, DateTimeOffset UploadedAt,
    bool CanDelete);

public sealed record AttachmentFile(Stream Content, string ContentType, string FileName);

public sealed record UploadAttachmentCommand(
    Guid WorkOrderId, AttachmentKind Kind, string FileName, string ContentType, long Length, Stream Content);

public sealed record ListAttachmentsQuery(Guid WorkOrderId);

public sealed record GetAttachmentFileQuery(Guid Id);

public sealed record DeleteAttachmentCommand(Guid Id);

/// <summary>US-TAPP-06 photos, US-TAPP-08 signatures; documents are for the office.</summary>
public sealed class UploadAttachmentHandler(
    IAppDbContext db, ICurrentUser user, IFileStorage files, TimeProvider clock, AttachmentReader reader)
    : ICommandHandler<UploadAttachmentCommand, Result<AttachmentDto>>
{
    public async Task<Result<AttachmentDto>> Handle(UploadAttachmentCommand cmd, CancellationToken ct)
    {
        if (cmd.Kind == AttachmentKind.Document && !user.IsOffice()) return Errors.Forbidden;

        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(cmd.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;

        var created = Attachment.Create(found.Value, cmd.Kind, cmd.FileName, cmd.ContentType, cmd.Length, user.UserId, clock.GetUtcNow());
        if (created.IsFailure) return created.Error;
        var attachment = created.Value;

        await files.SaveAsync(cmd.Content, attachment.StorageKey, ct);
        db.Attachments.Add(attachment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await files.DeleteAsync(attachment.StorageKey, CancellationToken.None);
            throw;
        }
        return (await reader.ReadAsync(db.Attachments.Where(a => a.Id == attachment.Id), found.Value, ct)).Single();
    }
}

public sealed class ListAttachmentsHandler(IAppDbContext db, ICurrentUser user, AttachmentReader reader)
    : IQueryHandler<ListAttachmentsQuery, Result<IReadOnlyList<AttachmentDto>>>
{
    public async Task<Result<IReadOnlyList<AttachmentDto>>> Handle(ListAttachmentsQuery query, CancellationToken ct)
    {
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(query.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;
        return Result.Success(await reader.ReadAsync(db.Attachments.Where(a => a.WorkOrderId == query.WorkOrderId), found.Value, ct));
    }
}

public sealed class GetAttachmentFileHandler(IAppDbContext db, ICurrentUser user, IFileStorage files)
    : IQueryHandler<GetAttachmentFileQuery, Result<AttachmentFile>>
{
    public async Task<Result<AttachmentFile>> Handle(GetAttachmentFileQuery query, CancellationToken ct)
    {
        var attachment = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == query.Id, ct);
        if (attachment is null) return AttachmentErrors.NotFound;
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(attachment.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;

        var content = await files.OpenReadAsync(attachment.StorageKey, ct);
        if (content is null) return AttachmentErrors.NotFound;
        return new AttachmentFile(content, attachment.ContentType, attachment.FileName);
    }
}

public sealed class DeleteAttachmentHandler(IAppDbContext db, ICurrentUser user, IFileStorage files)
    : ICommandHandler<DeleteAttachmentCommand, Result>
{
    public async Task<Result> Handle(DeleteAttachmentCommand cmd, CancellationToken ct)
    {
        var attachment = await db.Attachments.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct);
        if (attachment is null) return AttachmentErrors.NotFound;
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(attachment.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;

        var allowed = attachment.CanDelete(found.Value, user.UserId);
        if (allowed.IsFailure) return allowed;

        db.Attachments.Remove(attachment);
        await db.SaveChangesAsync(ct);
        await files.DeleteAsync(attachment.StorageKey, ct);
        return Result.Success();
    }
}

public sealed class AttachmentReader(ICurrentUser user, IIdentityService identity)
{
    public async Task<IReadOnlyList<AttachmentDto>> ReadAsync(IQueryable<Attachment> attachments, WorkOrder workOrder, CancellationToken ct)
    {
        var rows = await attachments.AsNoTracking().OrderBy(a => a.UploadedAt).ThenBy(a => a.Id).ToListAsync(ct);
        var people = await identity.FindByIdsAsync(rows.Select(a => a.UploadedBy).Distinct(), ct);
        return rows.Select(a => new AttachmentDto(a.Id, a.Kind, a.FileName, a.ContentType, a.SizeBytes,
                people.TryGetValue(a.UploadedBy, out var p) ? p.FullName : "", a.UploadedAt,
                a.CanDelete(workOrder, user.UserId).IsSuccess))
            .ToList();
    }
}
