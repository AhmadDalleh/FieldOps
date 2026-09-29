using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Sites;

public sealed record DeactivateSiteCommand(Guid Id);

public sealed class DeactivateSiteHandler(IAppDbContext db) : ICommandHandler<DeactivateSiteCommand, Result>
{
    public async Task<Result> Handle(DeactivateSiteCommand cmd, CancellationToken ct)
    {
        var site = await db.Sites.FirstOrDefaultAsync(s => s.Id == cmd.Id, ct);
        if (site is null) return CustomerErrors.SiteNotFound;

        // TODO(P4): return 409 when the site has open work orders (US-SITE-02).
        site.Deactivate();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
