using FieldOps.Application.Abstractions;
using FieldOps.Application.Common;
using FieldOps.Domain.Common;
using FieldOps.Domain.Identity;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.WorkOrders;

/// <summary>Office staff reach every work order; a technician only the ones assigned to them ("T*" in docs/05-api.md).</summary>
public static class WorkOrderAccess
{
    public static bool IsOffice(this ICurrentUser user) => user.Role is Role.Admin or Role.Dispatcher;

    public static bool CanAccess(this ICurrentUser user, WorkOrder workOrder) =>
        user.IsOffice() || (user.TechnicianId is { } own && workOrder.AssignedTechnicianId == own);

    /// <summary>Loads a work order and checks that the current user may reach it.</summary>
    public static async Task<Result<WorkOrder>> FindAccessibleAsync(
        this IQueryable<WorkOrder> workOrders, Guid id, ICurrentUser user, CancellationToken ct)
    {
        var workOrder = await workOrders.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workOrder is null) return WorkOrderErrors.NotFound;
        return user.CanAccess(workOrder) ? workOrder : Errors.Forbidden;
    }

    /// <summary>Saves, turning an optimistic-concurrency clash on the work order into a 409.</summary>
    public static async Task<Result> SaveAsync(this IAppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return WorkOrderErrors.ConcurrencyConflict;
        }
    }
}
