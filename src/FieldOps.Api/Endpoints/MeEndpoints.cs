using FieldOps.Api.Common;
using FieldOps.Application.Features.Me;

namespace FieldOps.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me").WithTags("Technician app").RequireAuthorization(Policies.TechnicianOnly);

        group.MapGet("/jobs", async (JobDay? day, MyJobsHandler handler, CancellationToken ct) =>
            (await handler.Handle(new MyJobsQuery(day ?? JobDay.Today), ct)).ToHttp());

        group.MapGet("/van-stock", async (VanStockHandler handler, CancellationToken ct) =>
            (await handler.Handle(new VanStockQuery(), ct)).ToHttp());

        return app;
    }
}
