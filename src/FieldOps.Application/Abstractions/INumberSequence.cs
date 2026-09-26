namespace FieldOps.Application.Abstractions;

public interface INumberSequence
{
    /// <summary>Returns the next value of the named sequence inside the current transaction.</summary>
    Task<long> NextAsync(string name, CancellationToken ct);
}
