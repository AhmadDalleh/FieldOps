using FieldOps.Domain.Common;

namespace FieldOps.Api.Common;

public static class ResultExtensions
{
    public static IResult ToProblem(this Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError,
        };

        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Details is not null) extensions["errors"] = error.Details;

        return Results.Problem(
            type: $"https://fieldops/errors/{error.Type.ToString().ToLowerInvariant()}",
            title: error.Message,
            statusCode: status,
            extensions: extensions);
    }

    public static IResult ToHttp(this Result result) =>
        result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();

    public static IResult ToHttp<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();

    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();
}
