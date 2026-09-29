using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Customers;

public sealed record ListContactsQuery(Guid CustomerId);

public sealed class ListContactsHandler(IAppDbContext db) : IQueryHandler<ListContactsQuery, Result<IReadOnlyList<ContactDto>>>
{
    public async Task<Result<IReadOnlyList<ContactDto>>> Handle(ListContactsQuery query, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == query.CustomerId, ct)) return CustomerErrors.NotFound;

        return await db.CustomerContacts.AsNoTracking()
            .Where(c => c.CustomerId == query.CustomerId)
            .OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Name)
            .Select(c => new ContactDto(c.Id, c.Name, c.Email, c.Phone, c.JobTitle, c.IsPrimary))
            .ToListAsync(ct);
    }
}
