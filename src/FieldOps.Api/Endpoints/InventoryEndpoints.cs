using FieldOps.Api.Common;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Inventory;
using FieldOps.Domain.Inventory;

namespace FieldOps.Api.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var parts = app.MapGroup("/api/parts").WithTags("Inventory").RequireAuthorization(Policies.OfficeStaff);

        parts.MapGet("/", async (int? page, int? pageSize, string? search, string? sort, bool? includeInactive, bool? lowOnly,
                ListPartsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(new ListPartsQuery(
                new PageRequest(page ?? 1, pageSize ?? 20, search, sort), includeInactive ?? false, lowOnly ?? false), ct)));

        parts.MapGet("/{id:guid}", async (Guid id, GetPartHandler handler, CancellationToken ct) =>
            (await handler.Handle(new GetPartQuery(id), ct)).ToHttp());

        parts.MapPost("/", async (PartInput input, CreatePartHandler handler, CancellationToken ct) =>
                (await handler.Handle(new CreatePartCommand(input), ct)).ToHttp(p => Results.Created($"/api/parts/{p.Id}", p)))
            .Validate<PartInput>()
            .RequireAuthorization(Policies.AdminOnly);

        parts.MapPut("/{id:guid}", async (Guid id, PartInput input, UpdatePartHandler handler, CancellationToken ct) =>
                (await handler.Handle(new UpdatePartCommand(id, input), ct)).ToHttp())
            .Validate<PartInput>()
            .RequireAuthorization(Policies.AdminOnly);

        app.MapGet("/api/stock-locations", async (ListStockLocationsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new ListStockLocationsQuery(), ct)))
            .WithTags("Inventory")
            .RequireAuthorization(Policies.OfficeStaff);

        var stock = app.MapGroup("/api/stock").WithTags("Inventory").RequireAuthorization(Policies.OfficeStaff);

        stock.MapGet("/", async (Guid? locationId, bool? lowOnly, GetStockHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(new GetStockQuery(locationId, lowOnly ?? false), ct)));

        stock.MapPost("/receive", async (ReceiveInput input, ReceiveStockHandler handler, CancellationToken ct) =>
                (await handler.Handle(new ReceiveStockCommand(input), ct)).ToHttp())
            .Validate<ReceiveInput>();

        stock.MapPost("/transfer", async (TransferInput input, TransferStockHandler handler, CancellationToken ct) =>
                (await handler.Handle(new TransferStockCommand(input), ct)).ToHttp())
            .Validate<TransferInput>();

        stock.MapPost("/adjust", async (AdjustInput input, AdjustStockHandler handler, CancellationToken ct) =>
                (await handler.Handle(new AdjustStockCommand(input), ct)).ToHttp())
            .Validate<AdjustInput>()
            .RequireAuthorization(Policies.AdminOnly);

        stock.MapGet("/movements", async (int? page, int? pageSize, Guid? partId, StockMovementType? type, Guid? locationId,
                DateOnly? from, DateOnly? to, ListMovementsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(new ListMovementsQuery(
                new PageRequest(page ?? 1, pageSize ?? 50), partId, type, locationId, from, to), ct)));

        return app;
    }
}
