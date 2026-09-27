using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using FieldOps.Domain.WorkOrders;
using Shouldly;

namespace FieldOps.Application.Tests.WorkOrders;

public class WorkOrderNoteTests(PostgresFixture fixture) : TestBase(fixture)
{
    private Task<Result<NoteDto>> Add(Guid id, string body, bool isInternal = false) =>
        Resolve<AddNoteHandler>().Handle(new AddNoteCommand(id, new NoteInput(body, isInternal)), default);

    private Task<Result<NoteDto>> Edit(Guid id, Guid noteId, string body) =>
        Resolve<EditNoteHandler>().Handle(new EditNoteCommand(id, noteId, new NoteInput(body, false)), default);

    private async Task<IReadOnlyList<NoteDto>> List(Guid id) =>
        (await Resolve<ListNotesHandler>().Handle(new ListNotesQuery(id), default)).Value;

    [Fact]
    public async Task Notes_are_listed_oldest_first_with_author_and_internal_flag()
    {
        var office = await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);
        await Add(wo.Id, "Customer prefers mornings");
        Fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        await Add(wo.Id, "Gate code 4411", isInternal: true);

        var notes = await List(wo.Id);

        notes.Select(n => (n.Body, n.IsInternal)).ShouldBe([("Customer prefers mornings", false), ("Gate code 4411", true)]);
        notes.ShouldAllBe(n => n.AuthorName == office.FullName && n.CanEdit);
    }

    [Fact]
    public async Task Author_can_edit_within_fifteen_minutes_but_not_after()
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);
        var note = (await Add(wo.Id, "Typo")).Value;

        Fixture.Clock.Advance(TimeSpan.FromMinutes(14));
        (await Edit(wo.Id, note.Id, "Fixed")).Value.Body.ShouldBe("Fixed");

        Fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        (await Edit(wo.Id, note.Id, "Too late")).Error.ShouldBe(WorkOrderErrors.NoteEditWindowClosed);
        (await List(wo.Id)).Single().CanEdit.ShouldBeFalse();
    }

    [Fact]
    public async Task Only_the_author_can_edit()
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);
        var note = (await Add(wo.Id, "Mine")).Value;
        await SignInOffice(Role.Admin);

        (await Edit(wo.Id, note.Id, "Changed")).Error.ShouldBe(WorkOrderErrors.NoteNotYours);
        (await List(wo.Id)).Single().CanEdit.ShouldBeFalse();
    }

    [Fact]
    public async Task Assigned_technician_can_add_notes_and_others_cannot()
    {
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);
        var (techUser, tech) = await GivenTechnician();
        var (otherUser, other) = await GivenTechnician();
        await Assign(wo.Id, tech);

        Fixture.CurrentUser.SignInAs(techUser, Role.Technician, tech);
        (await Add(wo.Id, "Arrived, unit is iced")).IsSuccess.ShouldBeTrue();

        Fixture.CurrentUser.SignInAs(otherUser, Role.Technician, other);
        (await Add(wo.Id, "Nosy")).Error.ShouldBe(Application.Common.Errors.Forbidden);
        (await Resolve<ListNotesHandler>().Handle(new ListNotesQuery(wo.Id), default)).Error.ShouldBe(Application.Common.Errors.Forbidden);
    }

    [Fact]
    public async Task Editing_a_note_of_another_work_order_returns_not_found()
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var first = await GivenWorkOrder(c, s);
        var second = await GivenWorkOrder(c, s);
        var note = (await Add(first.Id, "On the first")).Value;

        (await Edit(second.Id, note.Id, "Moved")).Error.ShouldBe(WorkOrderErrors.NoteNotFound);
    }
}
