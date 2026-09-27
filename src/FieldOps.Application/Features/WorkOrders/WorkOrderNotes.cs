using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.WorkOrders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

public sealed record NoteDto(
    Guid Id, Guid AuthorId, string AuthorName, string Body, bool IsInternal, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
    bool CanEdit);

public sealed record NoteInput(string Body, bool IsInternal);

public sealed class NoteInputValidator : AbstractValidator<NoteInput>
{
    public NoteInputValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
}

public sealed record ListNotesQuery(Guid WorkOrderId);

public sealed record AddNoteCommand(Guid WorkOrderId, NoteInput Input);

public sealed record EditNoteCommand(Guid WorkOrderId, Guid NoteId, NoteInput Input);

public sealed class ListNotesHandler(IAppDbContext db, ICurrentUser user, NoteReader reader)
    : IQueryHandler<ListNotesQuery, Result<IReadOnlyList<NoteDto>>>
{
    public async Task<Result<IReadOnlyList<NoteDto>>> Handle(ListNotesQuery query, CancellationToken ct)
    {
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(query.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;
        return Result<IReadOnlyList<NoteDto>>.Success(await reader.ReadAsync(db.WorkOrderNotes.Where(n => n.WorkOrderId == query.WorkOrderId), ct));
    }
}

public sealed class AddNoteHandler(IAppDbContext db, ICurrentUser user, NoteReader reader)
    : ICommandHandler<AddNoteCommand, Result<NoteDto>>
{
    public async Task<Result<NoteDto>> Handle(AddNoteCommand cmd, CancellationToken ct)
    {
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(cmd.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;

        var note = WorkOrderNote.Create(cmd.WorkOrderId, user.UserId, cmd.Input.Body.Trim(), cmd.Input.IsInternal);
        db.WorkOrderNotes.Add(note);
        await db.SaveChangesAsync(ct);
        return (await reader.ReadAsync(db.WorkOrderNotes.Where(n => n.Id == note.Id), ct))[0];
    }
}

public sealed class EditNoteHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock, NoteReader reader)
    : ICommandHandler<EditNoteCommand, Result<NoteDto>>
{
    public async Task<Result<NoteDto>> Handle(EditNoteCommand cmd, CancellationToken ct)
    {
        var found = await db.WorkOrders.AsNoTracking().FindAccessibleAsync(cmd.WorkOrderId, user, ct);
        if (found.IsFailure) return found.Error;

        var note = await db.WorkOrderNotes.FirstOrDefaultAsync(n => n.Id == cmd.NoteId && n.WorkOrderId == cmd.WorkOrderId, ct);
        if (note is null) return WorkOrderErrors.NoteNotFound;

        var result = note.Edit(user.UserId, cmd.Input.Body.Trim(), cmd.Input.IsInternal, clock.GetUtcNow());
        if (result.IsFailure) return result.Error;

        await db.SaveChangesAsync(ct);
        return (await reader.ReadAsync(db.WorkOrderNotes.Where(n => n.Id == note.Id), ct))[0];
    }
}

public sealed class NoteReader(IIdentityService identity, ICurrentUser user, TimeProvider clock)
{
    public async Task<IReadOnlyList<NoteDto>> ReadAsync(IQueryable<WorkOrderNote> notes, CancellationToken ct)
    {
        var rows = await notes.AsNoTracking().OrderBy(n => n.CreatedAt).ThenBy(n => n.Id).ToListAsync(ct);
        var authors = await identity.FindByIdsAsync(rows.Select(n => n.AuthorId), ct);
        var now = clock.GetUtcNow();

        return rows
            .Select(n => new NoteDto(n.Id, n.AuthorId, authors.TryGetValue(n.AuthorId, out var a) ? a.FullName : "", n.Body,
                n.IsInternal, n.CreatedAt, n.UpdatedAt, n.AuthorId == user.UserId && now - n.CreatedAt <= WorkOrderNote.EditWindow))
            .ToList();
    }
}
