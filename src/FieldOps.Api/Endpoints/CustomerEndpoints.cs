using FieldOps.Api.Common;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Sites;

namespace FieldOps.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers").WithTags("Customers").RequireAuthorization(Policies.OfficeStaff);

        group.MapGet("/", async (int? page, int? pageSize, string? search, string? sort, bool? includeInactive,
                ListCustomersHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(
                new ListCustomersQuery(new PageRequest(page ?? 1, pageSize ?? 20, search, sort), includeInactive ?? false), ct)));

        group.MapPost("/", async (CustomerInput input, bool? force, CreateCustomerHandler handler, CancellationToken ct) =>
                (await handler.Handle(new CreateCustomerCommand(input, force ?? false), ct))
                    .ToHttp(customer => Results.Created($"/api/customers/{customer.Id}", customer)))
            .Validate<CustomerInput>();

        group.MapGet("/{id:guid}", async (Guid id, GetCustomerHandler handler, CancellationToken ct) =>
            (await handler.Handle(new GetCustomerQuery(id), ct)).ToHttp());

        group.MapPut("/{id:guid}", async (Guid id, CustomerInput input, UpdateCustomerHandler handler, CancellationToken ct) =>
                (await handler.Handle(new UpdateCustomerCommand(id, input), ct)).ToHttp())
            .Validate<CustomerInput>();

        group.MapPost("/{id:guid}/deactivate", async (Guid id, DeactivateCustomerHandler handler, CancellationToken ct) =>
            (await handler.Handle(new DeactivateCustomerCommand(id), ct)).ToHttp());

        group.MapGet("/{id:guid}/contacts", async (Guid id, ListContactsHandler handler, CancellationToken ct) =>
            (await handler.Handle(new ListContactsQuery(id), ct)).ToHttp());

        group.MapPost("/{id:guid}/contacts", async (Guid id, ContactInput input, AddContactHandler handler, CancellationToken ct) =>
                (await handler.Handle(new AddContactCommand(id, input), ct))
                    .ToHttp(contact => Results.Created($"/api/customers/{id}/contacts/{contact.Id}", contact)))
            .Validate<ContactInput>();

        group.MapPut("/{id:guid}/contacts/{contactId:guid}", async (Guid id, Guid contactId, ContactInput input,
                    UpdateContactHandler handler, CancellationToken ct) =>
                (await handler.Handle(new UpdateContactCommand(id, contactId, input), ct)).ToHttp())
            .Validate<ContactInput>();

        group.MapDelete("/{id:guid}/contacts/{contactId:guid}", async (Guid id, Guid contactId, DeleteContactHandler handler, CancellationToken ct) =>
            (await handler.Handle(new DeleteContactCommand(id, contactId), ct)).ToHttp());

        group.MapGet("/{id:guid}/sites", async (Guid id, bool? includeInactive, ListSitesHandler handler, CancellationToken ct) =>
            (await handler.Handle(new ListSitesQuery(id, includeInactive ?? false), ct)).ToHttp());

        group.MapPost("/{id:guid}/sites", async (Guid id, SiteInput input, AddSiteHandler handler, CancellationToken ct) =>
                (await handler.Handle(new AddSiteCommand(id, input), ct))
                    .ToHttp(site => Results.Created($"/api/sites/{site.Id}", site)))
            .Validate<SiteInput>();

        return app;
    }
}
