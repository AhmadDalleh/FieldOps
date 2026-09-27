using FieldOps.Api.Common;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Invoices;
using FieldOps.Domain.Invoicing;

namespace FieldOps.Api.Endpoints;

public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/work-orders/{id:guid}/invoice", async (Guid id, GenerateInvoiceHandler handler, CancellationToken ct) =>
                (await handler.Handle(new GenerateInvoiceCommand(id), ct)).ToHttp(i => Results.Created($"/api/invoices/{i.Id}", i)))
            .WithTags("Invoices")
            .RequireAuthorization(Policies.OfficeStaff);

        var group = app.MapGroup("/api/invoices").WithTags("Invoices").RequireAuthorization(Policies.OfficeStaff);

        group.MapGet("/", async (int? page, int? pageSize, string? search, InvoiceStatus? status, Guid? customerId, DateOnly? from,
                DateOnly? to, bool? overdue, ListInvoicesHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(new ListInvoicesQuery(
                new PageRequest(page ?? 1, pageSize ?? 20, search), status, customerId, from, to, overdue ?? false), ct)));

        group.MapGet("/{id:guid}", async (Guid id, GetInvoiceHandler handler, CancellationToken ct) =>
            (await handler.Handle(new GetInvoiceQuery(id), ct)).ToHttp());

        group.MapDelete("/{id:guid}", async (Guid id, DeleteDraftInvoiceHandler handler, CancellationToken ct) =>
            (await handler.Handle(new DeleteDraftInvoiceCommand(id), ct)).ToHttp());

        group.MapPost("/{id:guid}/lines", async (Guid id, InvoiceLineInput input, AddInvoiceLineHandler handler, CancellationToken ct) =>
                (await handler.Handle(new AddInvoiceLineCommand(id, input), ct)).ToHttp())
            .Validate<InvoiceLineInput>();

        group.MapPut("/{id:guid}/lines/{lineId:guid}", async (Guid id, Guid lineId, InvoiceLineInput input,
                UpdateInvoiceLineHandler handler, CancellationToken ct) =>
                (await handler.Handle(new UpdateInvoiceLineCommand(id, lineId, input), ct)).ToHttp())
            .Validate<InvoiceLineInput>();

        group.MapDelete("/{id:guid}/lines/{lineId:guid}", async (Guid id, Guid lineId, RemoveInvoiceLineHandler handler,
                CancellationToken ct) =>
            (await handler.Handle(new RemoveInvoiceLineCommand(id, lineId), ct)).ToHttp());

        group.MapPost("/{id:guid}/issue", async (Guid id, IssueInvoiceHandler handler, CancellationToken ct) =>
                (await handler.Handle(new IssueInvoiceCommand(id), ct)).ToHttp())
            .RequireAuthorization(Policies.AdminOnly);

        group.MapPost("/{id:guid}/mark-paid", async (Guid id, MarkPaidInput input, MarkInvoicePaidHandler handler, CancellationToken ct) =>
                (await handler.Handle(new MarkInvoicePaidCommand(id, input), ct)).ToHttp())
            .Validate<MarkPaidInput>()
            .RequireAuthorization(Policies.AdminOnly);

        group.MapPost("/{id:guid}/void", async (Guid id, VoidInvoiceInput input, VoidInvoiceHandler handler, CancellationToken ct) =>
                (await handler.Handle(new VoidInvoiceCommand(id, input), ct)).ToHttp())
            .Validate<VoidInvoiceInput>()
            .RequireAuthorization(Policies.AdminOnly);

        group.MapGet("/{id:guid}/pdf", async (Guid id, GetInvoicePdfHandler handler, CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetInvoicePdfQuery(id), ct);
            return result.IsSuccess
                ? Results.Stream(result.Value.Content, "application/pdf", result.Value.FileName)
                : result.Error.ToProblem();
        });

        return app;
    }
}
