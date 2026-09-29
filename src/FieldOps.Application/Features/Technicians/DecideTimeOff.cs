using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Technicians;

public sealed record DecideTimeOffCommand(Guid Id, bool Approve);

public sealed class DecideTimeOffHandler(IAppDbContext db, TimeOffReader reader)
    : ICommandHandler<DecideTimeOffCommand, Result<TimeOffDecision>>
{
    public async Task<Result<TimeOffDecision>> Handle(DecideTimeOffCommand cmd, CancellationToken ct)
    {
        var timeOff = await db.TimeOffs.FirstOrDefaultAsync(t => t.Id == cmd.Id, ct);
        if (timeOff is null) return TechnicianErrors.TimeOffNotFound;

        var result = cmd.Approve ? timeOff.Approve() : timeOff.Reject();
        if (result.IsFailure) return result.Error;

        await db.SaveChangesAsync(ct);

        // TODO(P5): list the technician's scheduled jobs that the approved time off overlaps (US-TEC-03 AC3).
        IReadOnlyList<ConflictingJob> conflicts = [];

        var dto = (await reader.ReadAsync(db.TimeOffs.Where(t => t.Id == cmd.Id), ct))[0];
        return new TimeOffDecision(dto, conflicts);
    }
}
