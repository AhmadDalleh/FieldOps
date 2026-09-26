namespace FieldOps.Domain.Common;

public enum ErrorType { Validation, NotFound, Conflict, Forbidden, Unauthorized }

public sealed record Error(string Code, string Message, ErrorType Type, IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, message, ErrorType.Validation, details);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
}
