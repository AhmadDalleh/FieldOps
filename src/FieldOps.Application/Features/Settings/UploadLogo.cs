using FieldOps.Application.Abstractions;
using FieldOps.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Settings;

public sealed record UploadLogoCommand(Stream Content, string ContentType, long Length);

public sealed class UploadLogoHandler(IAppDbContext db, IFileStorage files, TimeProvider clock)
    : ICommandHandler<UploadLogoCommand, Result>
{
    public const long MaxBytes = 10 * 1024 * 1024;
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp"];

    public async Task<Result> Handle(UploadLogoCommand cmd, CancellationToken ct)
    {
        if (!AllowedTypes.Contains(cmd.ContentType))
            return Error.Validation("Settings.InvalidLogoType", "The logo must be a JPEG, PNG or WebP image.");
        if (cmd.Length is <= 0 or > MaxBytes)
            return Error.Validation("Settings.InvalidLogoSize", "The logo must be between 1 byte and 10 MB.");

        var now = clock.GetUtcNow();
        var key = $"{now:yyyy}/{now:MM}/{Guid.CreateVersion7()}";
        await files.SaveAsync(cmd.Content, key, ct);

        var settings = await db.AppSettings.SingleAsync(ct);
        var previous = settings.LogoKey;
        settings.SetLogo(key);
        await db.SaveChangesAsync(ct);

        if (previous is not null) await files.DeleteAsync(previous, ct);
        return Result.Success();
    }
}
