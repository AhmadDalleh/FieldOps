using FieldOps.Domain.Common;

namespace FieldOps.Domain.Customers;

public static class CustomerErrors
{
    public static readonly Error NotFound = Error.NotFound("Customer.NotFound", "The customer was not found.");
    public static readonly Error ContactNotFound = Error.NotFound("Customer.ContactNotFound", "The contact was not found.");
    public static readonly Error SiteNotFound = Error.NotFound("Site.NotFound", "The site was not found.");

    public static readonly Error HasOpenWorkOrders = Error.Conflict(
        "Customer.HasOpenWorkOrders", "The customer has open work orders. Complete or cancel them first.");

    public static readonly Error SiteHasOpenWorkOrders = Error.Conflict(
        "Site.HasOpenWorkOrders", "The site has open work orders. Complete or cancel them first.");

    public static readonly Error DuplicatePhone = Error.Conflict(
        "Customer.DuplicatePhone",
        "A customer with this phone number already exists. Save again with force=true to create it anyway.");
}
