using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Customers;

public sealed record UpdateContactCommand(Guid CustomerId, Guid ContactId, ContactInput Input);

public sealed class UpdateContactHandler(IAppDbContext db) : ICommandHandler<UpdateContactCommand, Result<ContactDto>>
{
    public async Task<Result<ContactDto>> Handle(UpdateContactCommand cmd, CancellationToken ct)
    {
        var customer = await db.Customers.Include(c => c.Contacts).FirstOrDefaultAsync(c => c.Id == cmd.CustomerId, ct);
        if (customer is null) return CustomerErrors.NotFound;

        var result = customer.UpdateContact(cmd.ContactId, cmd.Input.ToDetails());
        if (result.IsFailure) return result.Error;

        await db.SaveChangesAsync(ct);
        return ContactDto.From(customer.Contacts.Single(c => c.Id == cmd.ContactId));
    }
}
