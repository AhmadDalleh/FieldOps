using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Technicians;

public sealed record RequestTimeOffCommand(TimeOffInput Input);

public sealed class RequestTimeOffHandler(IAppDbContext db, ICurrentUser user, TimeOffReader reader)
    : ICommandHandler<RequestTimeOffCommand, Result<TimeOffDto>>
{
    public async Task<Result<TimeOffDto>> Handle(RequestTimeOffCommand cmd, CancellationToken ct)
    {
        Guid technicianId;
        if (user.Role == Role.Technician)
        {
            if (user.TechnicianId is not { } own) return Errors.Forbidden;
            if (cmd.Input.TechnicianId is { } requested && requested != own) return Errors.Forbidden;
            technicianId = own;
        }
        else
        {
            if (cmd.Input.TechnicianId is not { } chosen)
                return Error.Validation("TimeOff.TechnicianRequired", "Choose the technician the time off is for.");
            if (!await db.Technicians.AnyAsync(t => t.Id == chosen, ct)) return TechnicianErrors.NotFound;
            technicianId = chosen;
        }

        var reason = string.IsNullOrWhiteSpace(cmd.Input.Reason) ? null : cmd.Input.Reason.Trim();
        var timeOff = TimeOff.Request(technicianId, cmd.Input.StartsAt, cmd.Input.EndsAt, reason);
        if (timeOff.IsFailure) return timeOff.Error;

        db.TimeOffs.Add(timeOff.Value);
        await db.SaveChangesAsync(ct);
        return (await reader.ReadAsync(db.TimeOffs.Where(t => t.Id == timeOff.Value.Id), ct))[0];
    }
}
