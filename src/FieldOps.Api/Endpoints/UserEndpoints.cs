using FieldOps.Api.Common;
using FieldOps.Application.Common;
using FieldOps.Application.Features.Users;
using FieldOps.Domain.Identity;
using FluentValidation;

namespace FieldOps.Api.Endpoints;

public static class UserEndpoints
{
    public sealed record UpdateUserRequest(string FullName, string Email, string? PhoneNumber, Role Role);

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization(Policies.AdminOnly);

        group.MapGet("/", async (int? page, int? pageSize, string? search, string? sort, ListUsersHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(new ListUsersQuery(new PageRequest(page ?? 1, pageSize ?? 20, search, sort)), ct)));

        group.MapGet("/{id:guid}", async (Guid id, GetUserHandler handler, CancellationToken ct) =>
            (await handler.Handle(new GetUserQuery(id), ct)).ToHttp());

        group.MapPost("/", async (CreateUserCommand cmd, CreateUserHandler handler, CancellationToken ct) =>
                (await handler.Handle(cmd, ct)).ToHttp(user => Results.Created($"/api/users/{user.Id}", user)))
            .Validate<CreateUserCommand>();

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest body, UpdateUserHandler handler, IValidator<UpdateUserCommand> validator, CancellationToken ct) =>
        {
            var cmd = new UpdateUserCommand(id, body.FullName, body.Email, body.PhoneNumber, body.Role);
            var validation = await validator.ValidateAsync(cmd, ct);
            return validation.IsValid ? (await handler.Handle(cmd, ct)).ToHttp() : validation.ToProblem();
        });

        group.MapPost("/{id:guid}/deactivate", async (Guid id, SetUserActiveHandler handler, CancellationToken ct) =>
            (await handler.Handle(new SetUserActiveCommand(id, false), ct)).ToHttp());

        group.MapPost("/{id:guid}/activate", async (Guid id, SetUserActiveHandler handler, CancellationToken ct) =>
            (await handler.Handle(new SetUserActiveCommand(id, true), ct)).ToHttp());

        return app;
    }
}
