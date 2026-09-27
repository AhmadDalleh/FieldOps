using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Customers;

/// <param name="Force">Save even when another customer has the same phone number.</param>
public sealed record CreateCustomerCommand(CustomerInput Input, bool Force = false);

public sealed class CreateCustomerHandler(IAppDbContext db, INumberSequence numbers)
    : ICommandHandler<CreateCustomerCommand, Result<CustomerDto>>
{
    public const string SequenceName = "Customer";

    public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand cmd, CancellationToken ct)
    {
        var details = cmd.Input.ToDetails();
        if (!cmd.Force && await db.Customers.AnyAsync(c => c.Phone == details.Phone, ct))
            return CustomerErrors.DuplicatePhone;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var next = await numbers.NextAsync(SequenceName, ct);
        var customer = Customer.Create($"C-{next:00000}", details);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return CustomerDto.From(customer);
    }
}
