using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Customers;

public sealed record GetCustomerQuery(Guid Id);

public sealed class GetCustomerHandler(IAppDbContext db) : IQueryHandler<GetCustomerQuery, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(GetCustomerQuery query, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == query.Id, ct);
        return customer is null ? CustomerErrors.NotFound : CustomerDto.From(customer);
    }
}
