using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Domain.Tests;

public class WorkOrderNoteTests
{
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly DateTimeOffset Created = new(2026, 10, 1, 6, 0, 0, TimeSpan.Zero);

    private static WorkOrderNote Note()
    {
        var note = WorkOrderNote.Create(Guid.NewGuid(), Author, "Gate code 4411", isInternal: true);
        note.CreatedAt = Created;
        return note;
    }

    [Fact]
    public void Author_can_edit_within_fifteen_minutes()
    {
        var note = Note();

        note.Edit(Author, "Gate code 4412", isInternal: false, Created.AddMinutes(15)).IsSuccess.ShouldBeTrue();

        note.Body.ShouldBe("Gate code 4412");
        note.IsInternal.ShouldBeFalse();
    }

    [Fact]
    public void Editing_after_fifteen_minutes_is_rejected()
    {
        var note = Note();

        note.Edit(Author, "Late", true, Created.AddMinutes(15).AddSeconds(1)).Error.ShouldBe(WorkOrderErrors.NoteEditWindowClosed);
        note.Body.ShouldBe("Gate code 4411");
    }

    [Fact]
    public void Only_the_author_can_edit()
    {
        Note().Edit(Guid.NewGuid(), "Mine now", true, Created).Error.ShouldBe(WorkOrderErrors.NoteNotYours);
    }
}
