using FieldOps.Application.Abstractions;
using FieldOps.Domain.Inventory;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Application.Features.Users;

/// <summary>Keeps the technician row and van stock location in step with a user's Technician role.</summary>
public sealed class TechnicianProvisioner(IAppDbContext db, INumberSequence numbers)
{
    public const string SequenceName = "Technician";

    public async Task EnsureActiveAsync(Guid userId, string fullName, string? phone, CancellationToken ct)
    {
        var technician = await db.Technicians.FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (technician is null)
        {
            var next = await numbers.NextAsync(SequenceName, ct);
            technician = Technician.Create(userId, $"TEC-{next:000}", phone);
            db.Technicians.Add(technician);
            db.StockLocations.Add(StockLocation.CreateVan(technician.Id, fullName));
            return;
        }

        technician.UpdatePhone(phone);
        await SetActiveAsync(technician, true, ct);
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken ct)
    {
        var technician = await db.Technicians.FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (technician is not null) await SetActiveAsync(technician, false, ct);
    }

    private async Task SetActiveAsync(Technician technician, bool active, CancellationToken ct)
    {
        var van = await db.StockLocations.FirstOrDefaultAsync(l => l.TechnicianId == technician.Id, ct);
        if (active)
        {
            technician.Activate();
            van?.Activate();
        }
        else
        {
            technician.Deactivate();
            van?.Deactivate();
        }
    }
}
