using FieldOps.Api.Common;
using FieldOps.Application.Features.Settings;

namespace FieldOps.Api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").WithTags("Settings");

        group.MapGet("/", async (GetSettingsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new GetSettingsQuery(), ct)))
            .RequireAuthorization(Policies.OfficeStaff);

        group.MapPut("/", async (UpdateSettingsCommand cmd, UpdateSettingsHandler handler, CancellationToken ct) =>
                (await handler.Handle(cmd, ct)).ToHttp())
            .Validate<UpdateSettingsCommand>()
            .RequireAuthorization(Policies.AdminOnly);

        group.MapPost("/logo", async (IFormFile file, UploadLogoHandler handler, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                return (await handler.Handle(new UploadLogoCommand(stream, file.ContentType, file.Length), ct)).ToHttp();
            })
            .DisableAntiforgery()
            .RequireAuthorization(Policies.AdminOnly);

        return app;
    }
}
