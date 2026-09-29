using FieldOps.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Technicians;

public sealed record ListSkillsQuery;

public sealed class ListSkillsHandler(IAppDbContext db) : IQueryHandler<ListSkillsQuery, IReadOnlyList<SkillDto>>
{
    public async Task<IReadOnlyList<SkillDto>> Handle(ListSkillsQuery query, CancellationToken ct) =>
        await db.Skills.AsNoTracking().OrderBy(s => s.Name).Select(s => new SkillDto(s.Id, s.Name)).ToListAsync(ct);
}
