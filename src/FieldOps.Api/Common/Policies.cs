using FieldOps.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace FieldOps.Api.Common;

public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string OfficeStaff = nameof(OfficeStaff);
    public const string TechnicianOnly = nameof(TechnicianOnly);

    public static AuthorizationBuilder AddFieldOpsPolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(AdminOnly, p => p.RequireRole(nameof(Role.Admin)))
        .AddPolicy(OfficeStaff, p => p.RequireRole(nameof(Role.Admin), nameof(Role.Dispatcher)))
        .AddPolicy(TechnicianOnly, p => p.RequireRole(nameof(Role.Technician)));
}
