using FieldOps.Api.Common;
using FieldOps.Application.Features.Technicians;
using FieldOps.Domain.Technicians;

namespace FieldOps.Api.Endpoints;

public static class TechnicianEndpoints
{
    public static IEndpointRouteBuilder MapTechnicianEndpoints(this IEndpointRouteBuilder app)
    {
        var technicians = app.MapGroup("/api/technicians").WithTags("Technicians");

        technicians.MapGet("/", async (DateOnly? date, bool? includeInactive, ListTechniciansHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new ListTechniciansQuery(date, includeInactive ?? false), ct)))
            .RequireAuthorization(Policies.OfficeStaff);

        technicians.MapGet("/{id:guid}", async (Guid id, GetTechnicianHandler handler, CancellationToken ct) =>
                (await handler.Handle(new GetTechnicianQuery(id), ct)).ToHttp())
            .RequireAuthorization(Policies.OfficeStaff);

        technicians.MapPut("/{id:guid}", async (Guid id, TechnicianInput input, UpdateTechnicianHandler handler, CancellationToken ct) =>
                (await handler.Handle(new UpdateTechnicianCommand(id, input), ct)).ToHttp())
            .Validate<TechnicianInput>()
            .RequireAuthorization(Policies.AdminOnly);

        var skills = app.MapGroup("/api/skills").WithTags("Skills");

        skills.MapGet("/", async (ListSkillsHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new ListSkillsQuery(), ct)))
            .RequireAuthorization(Policies.OfficeStaff);

        skills.MapPost("/", async (SkillInput input, CreateSkillHandler handler, CancellationToken ct) =>
                (await handler.Handle(new CreateSkillCommand(input), ct))
                    .ToHttp(skill => Results.Created($"/api/skills/{skill.Id}", skill)))
            .Validate<SkillInput>()
            .RequireAuthorization(Policies.AdminOnly);

        skills.MapPut("/{id:guid}", async (Guid id, SkillInput input, RenameSkillHandler handler, CancellationToken ct) =>
                (await handler.Handle(new RenameSkillCommand(id, input), ct)).ToHttp())
            .Validate<SkillInput>()
            .RequireAuthorization(Policies.AdminOnly);

        var timeOff = app.MapGroup("/api/time-off").WithTags("Time off");

        // Any signed-in user; the handlers limit technicians to their own requests.
        timeOff.MapGet("/", async (TimeOffStatus? status, Guid? technicianId, ListTimeOffHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.Handle(new ListTimeOffQuery(status, technicianId), ct)))
            .RequireAuthorization();

        timeOff.MapPost("/", async (TimeOffInput input, RequestTimeOffHandler handler, CancellationToken ct) =>
                (await handler.Handle(new RequestTimeOffCommand(input), ct))
                    .ToHttp(request => Results.Created($"/api/time-off/{request.Id}", request)))
            .Validate<TimeOffInput>()
            .RequireAuthorization();

        timeOff.MapPost("/{id:guid}/approve", async (Guid id, DecideTimeOffHandler handler, CancellationToken ct) =>
                (await handler.Handle(new DecideTimeOffCommand(id, Approve: true), ct)).ToHttp())
            .RequireAuthorization(Policies.OfficeStaff);

        timeOff.MapPost("/{id:guid}/reject", async (Guid id, DecideTimeOffHandler handler, CancellationToken ct) =>
                (await handler.Handle(new DecideTimeOffCommand(id, Approve: false), ct)).ToHttp())
            .RequireAuthorization(Policies.OfficeStaff);

        return app;
    }
}
