using FieldOps.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace FieldOps.Infrastructure.Files;

public sealed class LocalFileStorage(IConfiguration configuration) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(configuration["Files:Root"] ?? "data/files");

    public async Task SaveAsync(Stream content, string key, CancellationToken ct)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = PathFor(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        File.Delete(PathFor(key));
        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("The storage key escapes the storage root.", nameof(key));
        return path;
    }
}
