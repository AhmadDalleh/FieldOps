using FieldOps.Domain.Common;

namespace FieldOps.Domain.Assets;

public static class AssetErrors
{
    public static readonly Error NotFound = Error.NotFound("Asset.NotFound", "The asset was not found.");

    public static readonly Error DuplicateSerial = Error.Validation(
        "Asset.DuplicateSerial", "An asset with this manufacturer and serial number already exists.");

    public static readonly Error SiteNotAvailable = Error.Validation(
        "Asset.SiteNotAvailable", "Assets can only be added to an active site of an active customer.");
}
