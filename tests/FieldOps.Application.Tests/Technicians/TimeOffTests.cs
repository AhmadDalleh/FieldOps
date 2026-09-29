using FieldOps.Application.Common;
using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Tests.Infrastructure;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FieldOps.Application.Tests.Technicians;

public class TimeOffTests(PostgresFixture fixture) : TestBase(fixture)
{
    // Dubai is UTC+4, so 2026-10-02 08:00 Dubai is 04:00 UTC.
    private static readonly DateTimeOffset Morning = new(2026, 10, 2, 4, 0, 0, TimeSpan.Zero);

    private async Task<(Guid UserId, Guid TechnicianId)> SignedInTechnician()
    {
        var technician = await GivenTechnician();
        Fixture.CurrentUser.SignInAs(technician.UserId, Role.Technician, technician.TechnicianId);
        return technician;
    }

    private async Task SignedInAs(Role role) => Fixture.CurrentUser.SignInAs((await GivenUser(role)).Id, role);

    private Task<Domain.Common.Result<TimeOffDto>> Request(TimeOffInput input) =>
        Resolve<RequestTimeOffHandler>().Handle(new RequestTimeOffCommand(input), default);

    private Task<Domain.Common.Result<TimeOffDecision>> Decide(Guid id, bool approve) =>
        Resolve<DecideTimeOffHandler>().Handle(new DecideTimeOffCommand(id, approve), default);

    private Task<IReadOnlyList<TimeOffDto>> List(TimeOffStatus? status = null, Guid? technicianId = null) =>
        Resolve<ListTimeOffHandler>().Handle(new ListTimeOffQuery(status, technicianId), default);

    [Fact]
    public async Task Technician_requests_time_off_for_themselves()
    {
        var (_, technicianId) = await SignedInTechnician();

        var request = (await Request(new TimeOffInput(Morning, Morning.AddHours(9), " Doctor visit "))).Value;

        request.TechnicianId.ShouldBe(technicianId);
        request.TechnicianName.ShouldBe("Technician User");
        request.Reason.ShouldBe("Doctor visit");
        request.Status.ShouldBe(TimeOffStatus.Pending);
    }

    [Fact]
    public async Task Technician_cannot_request_time_off_for_someone_else()
    {
        var (_, other) = await GivenTechnician();
        await SignedInTechnician();

        (await Request(new TimeOffInput(Morning, Morning.AddHours(1), null, other))).Error.Type
            .ShouldBe(Domain.Common.ErrorType.Forbidden);
    }

    [Fact]
    public async Task Office_requests_time_off_on_behalf_of_a_technician()
    {
        var (_, technicianId) = await GivenTechnician();
        await SignedInAs(Role.Dispatcher);

        (await Request(new TimeOffInput(Morning, Morning.AddDays(2), "Annual leave", technicianId))).Value
            .TechnicianId.ShouldBe(technicianId);
    }

    [Fact]
    public async Task Office_must_choose_a_technician()
    {
        await SignedInAs(Role.Admin);

        (await Request(new TimeOffInput(Morning, Morning.AddHours(1), null))).Error.Code.ShouldBe("TimeOff.TechnicianRequired");
    }

    [Fact]
    public async Task Office_gets_not_found_for_an_unknown_technician()
    {
        await SignedInAs(Role.Admin);

        (await Request(new TimeOffInput(Morning, Morning.AddHours(1), null, Guid.NewGuid()))).Error
            .ShouldBe(TechnicianErrors.NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void End_must_be_after_start(int hours)
    {
        new TimeOffInputValidator().Validate(new TimeOffInput(Morning, Morning.AddHours(hours), null)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Office_approves_a_pending_request()
    {
        await SignedInTechnician();
        var request = (await Request(new TimeOffInput(Morning, Morning.AddHours(4), null))).Value;
        await SignedInAs(Role.Dispatcher);

        var decision = (await Decide(request.Id, approve: true)).Value;

        decision.TimeOff.Status.ShouldBe(TimeOffStatus.Approved);
        decision.ConflictingJobs.ShouldBeEmpty();
    }

    [Fact]
    public async Task Office_rejects_a_pending_request()
    {
        await SignedInTechnician();
        var request = (await Request(new TimeOffInput(Morning, Morning.AddHours(4), null))).Value;

        (await Decide(request.Id, approve: false)).Value.TimeOff.Status.ShouldBe(TimeOffStatus.Rejected);
    }

    [Fact]
    public async Task A_decided_request_cannot_be_decided_again()
    {
        await SignedInTechnician();
        var request = (await Request(new TimeOffInput(Morning, Morning.AddHours(4), null))).Value;
        await Decide(request.Id, approve: false);

        (await Decide(request.Id, approve: true)).Error.Code.ShouldBe("TimeOff.AlreadyDecided");
    }

    [Fact]
    public async Task Deciding_a_missing_request_returns_not_found()
    {
        (await Decide(Guid.NewGuid(), approve: true)).Error.ShouldBe(TechnicianErrors.TimeOffNotFound);
    }

    [Fact]
    public async Task Approval_lists_scheduled_jobs_the_time_off_overlaps()
    {
        var (techUser, tech) = await GivenTechnician();
        var (c, s) = await GivenCustomerAndSite();
        var inside = await GivenWorkOrder(c, s, title: "inside");
        var outside = await GivenWorkOrder(c, s, title: "outside");
        await Advance(inside.Id, (w, u, now) => w.Schedule(tech, Morning.AddHours(1), Morning.AddHours(2), u, now));
        await Advance(outside.Id, (w, u, now) => w.Schedule(tech, Morning.AddHours(9), Morning.AddHours(10), u, now));
        Fixture.CurrentUser.SignInAs(techUser, Role.Technician, tech);
        var request = (await Request(new TimeOffInput(Morning, Morning.AddHours(4), "Dentist"))).Value;
        await SignedInAs(Role.Dispatcher);

        var decision = (await Decide(request.Id, approve: true)).Value;

        var conflict = decision.ConflictingJobs.ShouldHaveSingleItem();
        conflict.Number.ShouldBe(inside.Number);
        conflict.ScheduledStart.ShouldBe(Morning.AddHours(1));
    }

    [Fact]
    public async Task Technician_only_sees_their_own_requests()
    {
        var (otherUser, otherTech) = await GivenTechnician();
        Fixture.CurrentUser.SignInAs(otherUser, Role.Technician, otherTech);
        await Request(new TimeOffInput(Morning, Morning.AddHours(1), "Other"));
        var (_, mine) = await SignedInTechnician();
        await Request(new TimeOffInput(Morning, Morning.AddHours(1), "Mine"));

        var requests = await List(technicianId: otherTech);

        requests.ShouldHaveSingleItem().TechnicianId.ShouldBe(mine);
    }

    [Fact]
    public async Task Office_filters_requests_by_status_and_technician()
    {
        var (userA, techA) = await GivenTechnician();
        var (userB, techB) = await GivenTechnician();
        Fixture.CurrentUser.SignInAs(userA, Role.Technician, techA);
        var approved = (await Request(new TimeOffInput(Morning, Morning.AddHours(1), null))).Value;
        await Request(new TimeOffInput(Morning.AddDays(1), Morning.AddDays(1).AddHours(1), null));
        Fixture.CurrentUser.SignInAs(userB, Role.Technician, techB);
        await Request(new TimeOffInput(Morning, Morning.AddHours(1), null));
        await SignedInAs(Role.Admin);
        await Decide(approved.Id, approve: true);

        (await List()).Count.ShouldBe(3);
        (await List(TimeOffStatus.Pending)).Count.ShouldBe(2);
        (await List(technicianId: techA)).Count.ShouldBe(2);
        (await List(TimeOffStatus.Approved, techA)).ShouldHaveSingleItem().Id.ShouldBe(approved.Id);
    }
}
