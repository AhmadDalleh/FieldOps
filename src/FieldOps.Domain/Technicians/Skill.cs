using FieldOps.Domain.Common;

namespace FieldOps.Domain.Technicians;

public sealed class Skill : AuditableEntity
{
    private Skill() { }

    public string Name { get; private set; } = null!;

    public static Skill Create(string name) => new() { Name = name };

    public void Rename(string name) => Name = name;
}
