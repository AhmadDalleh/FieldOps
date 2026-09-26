using FieldOps.Application.Abstractions;
using FieldOps.Domain.Identity;

namespace FieldOps.Application.Tests.Infrastructure;

public sealed class TestCurrentUser : ICurrentUser
{
    private Guid? _userId;

    public bool IsAuthenticated => _userId is not null;
    public Guid UserId => _userId ?? throw new InvalidOperationException("No user is signed in.");
    public Role? Role { get; private set; }
    public Guid? TechnicianId { get; private set; }

    public void SignInAs(Guid userId, Role role, Guid? technicianId = null)
    {
        _userId = userId;
        Role = role;
        TechnicianId = technicianId;
    }
}
