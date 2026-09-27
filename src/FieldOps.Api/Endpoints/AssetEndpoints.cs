using FieldOps.Api.Common;
using FieldOps.Application.Features.Assets;

namespace FieldOps.Api.Endpoints;

public static class AssetEndpoints
{
    public static IEndpointRouteBuilder MapAssetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/sites/{siteId:guid}/assets", async (Guid siteId, ListSiteAssetsHandler handler, CancellationToken ct) =>
                (await handler.Handle(new ListSiteAssetsQuery(siteId), ct)).ToHttp())
            .WithTags("Assets")
            .RequireAuthorization(Policies.OfficeStaff);

        app.MapPost("/api/sites/{siteId:guid}/assets", async (Guid siteId, AssetInput input, RegisterAssetHandler handler, CancellationToken ct) =>
                (await handler.Handle(new RegisterAssetCommand(siteId, input), ct))
                    .ToHttp(asset => Results.Created($"/api/assets/{asset.Id}", asset)))
            .Validate<AssetInput>()
            .WithTags("Assets")
            .RequireAuthorization(Policies.OfficeStaff);

        app.MapGet("/api/customers/{customerId:guid}/assets", async (Guid customerId, ListCustomerAssetsHandler handler, CancellationToken ct) =>
                (await handler.Handle(new ListCustomerAssetsQuery(customerId), ct)).ToHttp())
            .WithTags("Assets")
            .RequireAuthorization(Policies.OfficeStaff);

        var group = app.MapGroup("/api/assets").WithTags("Assets");

        // Technicians are allowed through; the handler limits them to assets on their own work orders.
        group.MapGet("/{id:guid}", async (Guid id, GetAssetHandler handler, CancellationToken ct) =>
                (await handler.Handle(new GetAssetQuery(id), ct)).ToHttp())
            .RequireAuthorization();

        group.MapPut("/{id:guid}", async (Guid id, AssetInput input, UpdateAssetHandler handler, CancellationToken ct) =>
                (await handler.Handle(new UpdateAssetCommand(id, input), ct)).ToHttp())
            .Validate<AssetInput>()
            .RequireAuthorization(Policies.OfficeStaff);

        // Technicians are allowed through; the handler limits them to assets on their own work orders.
        group.MapGet("/{id:guid}/history", async (Guid id, GetAssetHistoryHandler handler, CancellationToken ct) =>
                (await handler.Handle(new GetAssetHistoryQuery(id), ct)).ToHttp())
            .RequireAuthorization();

        return app;
    }
}
