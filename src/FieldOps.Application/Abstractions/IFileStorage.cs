namespace FieldOps.Application.Abstractions;

public interface IFileStorage
{
    Task SaveAsync(Stream content, string key, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
