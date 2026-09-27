using FieldOps.Api.Common;
using FieldOps.Application.Features.Notifications;

namespace FieldOps.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", async (bool? unreadOnly, int? take, ListNotificationsHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.Handle(new ListNotificationsQuery(unreadOnly ?? false, take ?? 30), ct)));

        group.MapPost("/{id:guid}/read", async (Guid id, MarkNotificationReadHandler handler, CancellationToken ct) =>
            (await handler.Handle(new MarkNotificationReadCommand(id), ct)).ToHttp());

        group.MapPost("/read-all", async (MarkAllNotificationsReadHandler handler, CancellationToken ct) =>
            (await handler.Handle(new MarkAllNotificationsReadCommand(), ct)).ToHttp());

        return app;
    }
}
