using FieldOps.Domain.Common;

namespace FieldOps.Domain.Customers;

public sealed class Site : AuditableEntity
{
    private Site() { }

    public Guid CustomerId { get; private init; }
    public string Name { get; private set; } = null!;
    public string AddressLine1 { get; private set; } = null!;
    public string? AddressLine2 { get; private set; }
    public string City { get; private set; } = null!;
    public string? Region { get; private set; }
    public string Country { get; private set; } = DefaultCountry;
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? AccessNotes { get; private set; }
    public bool IsActive { get; private set; } = true;

    public const string DefaultCountry = "AE";

    public static Result<Site> Create(Guid customerId, SiteDetails details)
    {
        var site = new Site { CustomerId = customerId };
        var result = site.Update(details);
        return result.IsSuccess ? site : result.Error;
    }

    public Result Update(SiteDetails details)
    {
        if (details.Latitude is < -90 or > 90)
            return Error.Validation("Site.InvalidLatitude", "Latitude must be between -90 and 90.");
        if (details.Longitude is < -180 or > 180)
            return Error.Validation("Site.InvalidLongitude", "Longitude must be between -180 and 180.");
        if (details.Latitude.HasValue != details.Longitude.HasValue)
            return Error.Validation("Site.IncompleteLocation", "Latitude and longitude must be given together.");

        Name = details.Name;
        AddressLine1 = details.AddressLine1;
        AddressLine2 = details.AddressLine2;
        City = details.City;
        Region = details.Region;
        Country = string.IsNullOrWhiteSpace(details.Country) ? DefaultCountry : details.Country;
        Latitude = details.Latitude;
        Longitude = details.Longitude;
        AccessNotes = details.AccessNotes;
        return Result.Success();
    }

    /// <remarks>The open-work-order rule (US-SITE-02) is checked by the handler once work orders exist.</remarks>
    public void Deactivate() => IsActive = false;
}

public sealed record SiteDetails(
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? Region,
    string? Country,
    double? Latitude,
    double? Longitude,
    string? AccessNotes);
