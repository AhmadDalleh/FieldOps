using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Customers;

public sealed record UpdateCustomerCommand(Guid Id, CustomerInput Input);

public sealed class UpdateCustomerHandler(IAppDbContext db) : ICommandHandler<UpdateCustomerCommand, Result<CustomerDto>>
{
    public async Task<Result<CustomerDto>> Handle(UpdateCustomerCommand cmd, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == cmd.Id, ct);
        if (customer is null) return CustomerErrors.NotFound;

        customer.Update(cmd.Input.ToDetails());
        await db.SaveChangesAsync(ct);
        return CustomerDto.From(customer);
    }
}
