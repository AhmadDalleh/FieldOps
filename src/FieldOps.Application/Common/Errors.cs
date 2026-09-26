using FieldOps.Domain.Common;

namespace FieldOps.Application.Common;

public static class Errors
{
    public static readonly Error Forbidden = Error.Forbidden("Forbidden", "You are not allowed to do this.");

    public static class Auth
    {
        public static readonly Error InvalidCredentials =
            Error.Unauthorized("Auth.InvalidCredentials", "The email or password is incorrect.");

        public static readonly Error InvalidRefreshToken =
            Error.Unauthorized("Auth.InvalidRefreshToken", "The session has expired. Please log in again.");
    }

    public static class User
    {
        public static readonly Error NotFound = Error.NotFound("User.NotFound", "The user was not found.");

        public static readonly Error EmailTaken = Error.Conflict("User.EmailTaken", "A user with this email already exists.");

        public static readonly Error CannotDeactivateSelf =
            Error.Conflict("User.CannotDeactivateSelf", "You cannot deactivate your own account.");
    }
}
