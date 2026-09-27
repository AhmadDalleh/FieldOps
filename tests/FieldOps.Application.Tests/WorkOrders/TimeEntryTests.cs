using FieldOps.Application.Features.WorkOrders;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.WorkOrders;

public class TimeEntryTests(PostgresFixture fixture) : TestBase(fixture)
{
    private async Task<Guid> GivenJobWorkedFor((Guid UserId, Guid TechnicianId) tech, TimeSpan worked)
    {
        await SignInOffice();
        var (c, s) = await GivenCustomerAndSite();
        var wo = await GivenWorkOrder(c, s);
        await Assign(wo.Id, tech.TechnicianId);
        await Advance(wo.Id, (w, u, now) => w.Dispatch(u, now));
        await Advance(wo.Id, (w, u, now) => w.Start(u, now));
        Fixture.Clock.Advance(worked);
        await Advance(wo.Id, (w, u, now) => w.Hold("Break", u, now));
        return wo.Id;
    }

    private async Task<TimeEntry> OnlyEntry(Guid workOrderId) => await NewDb().TimeEntries.SingleAsync(e => e.WorkOrderId == workOrderId);

    [Fact]
    public async Task Technician_lists_and_corrects_their_own_entry()
    {
        var tech = await GivenTechnician();
        var wo = await GivenJobWorkedFor(tech, TimeSpan.FromHours(1));
        var entry = await OnlyEntry(wo);
        SignInTechnician(tech);

        var listed = (await Resolve<ListTimeEntriesHandler>().Handle(new ListTimeEntriesQuery(wo), default)).Value.ShouldHaveSingleItem();
        listed.Type.ShouldBe(TimeEntryType.Work);
        listed.TechnicianName.ShouldBe("Technician User");
        listed.CanEdit.ShouldBeTrue();

        var corrected = (await Resolve<CorrectTimeEntryHandler>().Handle(new CorrectTimeEntryCommand(entry.Id,
            new TimeEntryInput(entry.StartedAt.AddMinutes(15), entry.EndedAt)), default)).Value.ShouldHaveSingleItem();
        corrected.DurationMinutes.ShouldBe(45);
    }

    [Fact]
    public async Task Entries_may_not_overlap_the_technicians_other_entries()
    {
        var tech = await GivenTechnician();
        var first = await GivenJobWorkedFor(tech, TimeSpan.FromHours(1));
        var second = await GivenJobWorkedFor(tech, TimeSpan.FromHours(1));
        var firstEntry = await OnlyEntry(first);
        var secondEntry = await OnlyEntry(second);
        SignInTechnician(tech);

        var result = await Resolve<CorrectTimeEntryHandler>().Handle(new CorrectTimeEntryCommand(secondEntry.Id,
            new TimeEntryInput(firstEntry.EndedAt!.Value.AddMinutes(-10), secondEntry.EndedAt)), default);

        result.Error.ShouldBe(WorkOrderErrors.TimeEntryOverlap);
    }

    [Fact]
    public async Task Office_may_correct_any_entry_but_nobody_after_completion()
    {
        var tech = await GivenTechnician();
        var wo = await GivenJobWorkedFor(tech, TimeSpan.FromHours(1));
        var entry = await OnlyEntry(wo);
        await SignInOffice();
        var input = new TimeEntryInput(entry.StartedAt, entry.EndedAt!.Value.AddMinutes(-30));

        (await Resolve<CorrectTimeEntryHandler>().Handle(new CorrectTimeEntryCommand(entry.Id, input), default)).IsSuccess.ShouldBeTrue();

        await Advance(wo, (w, u, now) => w.Resume(u, now));
        var signature = await GivenSignature(wo);
        await Advance(wo, (w, u, now) => w.Complete("Done", "Sara", signature, u, now));
        (await Resolve<CorrectTimeEntryHandler>().Handle(new CorrectTimeEntryCommand(entry.Id, input), default)).Error
            .ShouldBe(WorkOrderErrors.TimeEntriesLocked);
        (await Resolve<ListTimeEntriesHandler>().Handle(new ListTimeEntriesQuery(wo), default)).Value.ShouldAllBe(e => !e.CanEdit);
    }
}
