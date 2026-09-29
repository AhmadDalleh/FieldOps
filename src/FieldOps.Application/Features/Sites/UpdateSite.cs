using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Sites;

public sealed record UpdateSiteCommand(Guid Id, SiteInput Input);

public sealed class UpdateSiteHandler(IAppDbContext db) : ICommandHandler<UpdateSiteCommand, Result<SiteDto>>
{
    public async Task<Result<SiteDto>> Handle(UpdateSiteCommand cmd, CancellationToken ct)
    {
        var site = await db.Sites.FirstOrDefaultAsync(s => s.Id == cmd.Id, ct);
        if (site is null) return CustomerErrors.SiteNotFound;

        var result = site.Update(cmd.Input.ToDetails());
        if (result.IsFailure) return result.Error;

        await db.SaveChangesAsync(ct);
        return SiteDto.From(site);
    }
}
