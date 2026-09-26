using FieldOps.Api.Common;
using FieldOps.Application.Features.Auth;

namespace FieldOps.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginCommand cmd, LoginHandler handler, CancellationToken ct) =>
                (await handler.Handle(cmd, ct)).ToHttp())
            .Validate<LoginCommand>()
            .AllowAnonymous();

        group.MapPost("/refresh", async (RefreshCommand cmd, RefreshHandler handler, CancellationToken ct) =>
                (await handler.Handle(cmd, ct)).ToHttp())
            .Validate<RefreshCommand>()
            .AllowAnonymous();

        group.MapPost("/logout", async (LogoutCommand cmd, LogoutHandler handler, CancellationToken ct) =>
                (await handler.Handle(cmd, ct)).ToHttp())
            .Validate<LogoutCommand>()
            .RequireAuthorization();

        group.MapGet("/me", async (GetMeHandler handler, CancellationToken ct) =>
                (await handler.Handle(new GetMeQuery(), ct)).ToHttp())
            .RequireAuthorization();

        group.MapPost("/change-password", async (ChangePasswordCommand cmd, ChangePasswordHandler handler, CancellationToken ct) =>
                (await handler.Handle(cmd, ct)).ToHttp())
            .Validate<ChangePasswordCommand>()
            .RequireAuthorization();

        return app;
    }
}
