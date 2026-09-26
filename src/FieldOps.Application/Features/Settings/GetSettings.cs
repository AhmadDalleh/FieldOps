using FieldOps.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Settings;

public sealed record GetSettingsQuery;

public sealed class GetSettingsHandler(IAppDbContext db) : IQueryHandler<GetSettingsQuery, SettingsDto>
{
    public async Task<SettingsDto> Handle(GetSettingsQuery query, CancellationToken ct) =>
        SettingsDto.From(await db.AppSettings.AsNoTracking().SingleAsync(ct));
}
