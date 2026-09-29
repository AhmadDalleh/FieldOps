using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Technicians;

namespace FieldOps.Application.Features.Technicians;

public sealed record GetTechnicianQuery(Guid Id);

public sealed class GetTechnicianHandler(IAppDbContext db, TechnicianReader reader) : IQueryHandler<GetTechnicianQuery, Result<TechnicianDto>>
{
    public async Task<Result<TechnicianDto>> Handle(GetTechnicianQuery query, CancellationToken ct)
    {
        var found = await reader.ReadAsync(db.Technicians.Where(t => t.Id == query.Id), ct);
        return found.Count == 0 ? TechnicianErrors.NotFound : found[0];
    }
}
