using FieldOps.Api.Common;
using FieldOps.Application.Features.Sites;

namespace FieldOps.Api.Endpoints;

public static class SiteEndpoints
{
    public static IEndpointRouteBuilder MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sites").WithTags("Sites").RequireAuthorization(Policies.OfficeStaff);

        group.MapGet("/{id:guid}", async (Guid id, GetSiteHandler handler, CancellationToken ct) =>
            (await handler.Handle(new GetSiteQuery(id), ct)).ToHttp());

        group.MapPut("/{id:guid}", async (Guid id, SiteInput input, UpdateSiteHandler handler, CancellationToken ct) =>
                (await handler.Handle(new UpdateSiteCommand(id, input), ct)).ToHttp())
            .Validate<SiteInput>();

        group.MapPost("/{id:guid}/deactivate", async (Guid id, DeactivateSiteHandler handler, CancellationToken ct) =>
            (await handler.Handle(new DeactivateSiteCommand(id), ct)).ToHttp());

        return app;
    }
}
