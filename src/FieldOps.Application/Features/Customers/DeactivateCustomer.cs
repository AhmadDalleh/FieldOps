using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Customers;

public sealed record DeactivateCustomerCommand(Guid Id);

public sealed class DeactivateCustomerHandler(IAppDbContext db) : ICommandHandler<DeactivateCustomerCommand, Result>
{
    public async Task<Result> Handle(DeactivateCustomerCommand cmd, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct);
        if (customer is null) return CustomerErrors.NotFound;

        // TODO(P4): return 409 when the customer has open work orders (US-CUS-04 AC1).
        customer.Deactivate();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
