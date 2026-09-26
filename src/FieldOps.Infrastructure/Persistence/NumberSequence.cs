namespace FieldOps.Infrastructure.Persistence;

public sealed class NumberSequence
{
    public string Name { get; set; } = null!;
    public long NextValue { get; set; }
}
