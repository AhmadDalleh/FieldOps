using Microsoft.AspNetCore.Identity;

namespace FieldOps.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser() => Id = Guid.CreateVersion7();

    public string FullName { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
