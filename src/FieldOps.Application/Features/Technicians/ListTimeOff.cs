using FieldOps.Application.Abstractions;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Technicians;

namespace FieldOps.Application.Features.Technicians;

public sealed record ListTimeOffQuery(TimeOffStatus? Status = null, Guid? TechnicianId = null);

public sealed class ListTimeOffHandler(IAppDbContext db, ICurrentUser user, TimeOffReader reader)
    : IQueryHandler<ListTimeOffQuery, IReadOnlyList<TimeOffDto>>
{
    public Task<IReadOnlyList<TimeOffDto>> Handle(ListTimeOffQuery query, CancellationToken ct)
    {
        var timeOff = db.TimeOffs.AsQueryable();

        // Technicians only ever see their own requests.
        var technicianId = user.Role == Role.Technician ? user.TechnicianId ?? Guid.Empty : query.TechnicianId;
        if (technicianId is { } id) timeOff = timeOff.Where(t => t.TechnicianId == id);
        if (query.Status is { } status) timeOff = timeOff.Where(t => t.Status == status);

        return reader.ReadAsync(timeOff, ct);
    }
}
