using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FieldOps.Infrastructure.Persistence.Interceptors;

public sealed class AuditInterceptor(ICurrentUser currentUser, TimeProvider clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is not null) Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void Stamp(DbContext context)
    {
        var now = clock.GetUtcNow();
        Guid? userId = currentUser.IsAuthenticated ? currentUser.UserId : null;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }
    }
}
