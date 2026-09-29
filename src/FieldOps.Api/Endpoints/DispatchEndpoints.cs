using FieldOps.Api.Common;
using FieldOps.Application.Features.Dispatch;

namespace FieldOps.Api.Endpoints;

public static class DispatchEndpoints
{
    public static IEndpointRouteBuilder MapDispatchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dispatch/board", async (DateOnly? date, GetDispatchBoardHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new GetDispatchBoardQuery(date), ct)))
            .WithTags("Dispatch")
            .RequireAuthorization(Policies.OfficeStaff);

        return app;
    }
}
