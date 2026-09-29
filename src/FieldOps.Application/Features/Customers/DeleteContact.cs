using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Customers;

public sealed record DeleteContactCommand(Guid CustomerId, Guid ContactId);

public sealed class DeleteContactHandler(IAppDbContext db) : ICommandHandler<DeleteContactCommand, Result>
{
    public async Task<Result> Handle(DeleteContactCommand cmd, CancellationToken ct)
    {
        var customer = await db.Customers.Include(c => c.Contacts).FirstOrDefaultAsync(c => c.Id == cmd.CustomerId, ct);
        if (customer is null) return CustomerErrors.NotFound;

        var result = customer.RemoveContact(cmd.ContactId);
        if (result.IsFailure) return result;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
