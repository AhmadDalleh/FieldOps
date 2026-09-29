using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using FieldOps.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Customers;

public sealed record AddContactCommand(Guid CustomerId, ContactInput Input);

public sealed class AddContactHandler(IAppDbContext db) : ICommandHandler<AddContactCommand, Result<ContactDto>>
{
    public async Task<Result<ContactDto>> Handle(AddContactCommand cmd, CancellationToken ct)
    {
        var customer = await db.Customers.Include(c => c.Contacts).FirstOrDefaultAsync(c => c.Id == cmd.CustomerId, ct);
        if (customer is null) return CustomerErrors.NotFound;

        var contact = customer.AddContact(cmd.Input.ToDetails());
        await db.SaveChangesAsync(ct);
        return ContactDto.From(contact);
    }
}
