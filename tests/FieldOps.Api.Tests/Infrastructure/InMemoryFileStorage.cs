using System.Collections.Concurrent;
using FieldOps.Application.Abstractions;

namespace FieldOps.Api.Tests.Infrastructure;

public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();

    public async Task SaveAsync(Stream content, string key, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _files[key] = buffer.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) =>
        Task.FromResult<Stream?>(_files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        _files.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
