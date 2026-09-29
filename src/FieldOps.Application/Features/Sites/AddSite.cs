using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Sites;

public sealed record AddSiteCommand(Guid CustomerId, SiteInput Input);

public sealed class AddSiteHandler(IAppDbContext db) : ICommandHandler<AddSiteCommand, Result<SiteDto>>
{
    public async Task<Result<SiteDto>> Handle(AddSiteCommand cmd, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == cmd.CustomerId, ct)) return CustomerErrors.NotFound;

        var site = Site.Create(cmd.CustomerId, cmd.Input.ToDetails());
        if (site.IsFailure) return site.Error;

        db.Sites.Add(site.Value);
        await db.SaveChangesAsync(ct);
        return SiteDto.From(site.Value);
    }
}
