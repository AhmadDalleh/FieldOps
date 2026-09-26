using FieldOps.Domain.Identity;

namespace FieldOps.Application.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    Role? Role { get; }
    Guid? TechnicianId { get; }
}
