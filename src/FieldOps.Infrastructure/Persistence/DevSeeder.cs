using FieldOps.Application.Features.Settings;
using FieldOps.Application.Features.Users;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Technicians;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FieldOps.Infrastructure.Persistence;

/// <summary>Creates the development users and skills. Runs in Development only and is safe to run repeatedly.</summary>
public sealed class DevSeeder(
    AppDbContext db,
    CreateUserHandler createUser,
    UpdateSettingsHandler updateSettings,
    ILogger<DevSeeder> logger)
{
    public const string Password = "Pass123!";

    private static readonly (string FullName, string Email, Role Role)[] Users =
    [
        ("Admin User", "admin@fieldops.local", Role.Admin),
        ("Dispatcher User", "dispatcher@fieldops.local", Role.Dispatcher),
        ("Tech One", "tech1@fieldops.local", Role.Technician),
        ("Tech Two", "tech2@fieldops.local", Role.Technician),
        ("Tech Three", "tech3@fieldops.local", Role.Technician),
    ];

    private static readonly string[] Skills = ["HVAC", "Electrical", "Plumbing", "General maintenance"];

    public async Task SeedAsync(CancellationToken ct)
    {
        await db.Database.MigrateAsync(ct);

        if (!await db.Skills.AnyAsync(ct))
        {
            db.Skills.AddRange(Skills.Select(Skill.Create));
            await db.SaveChangesAsync(ct);
        }

        if (await db.Users.AnyAsync(ct)) return;

        foreach (var (fullName, email, role) in Users)
        {
            var result = await createUser.Handle(new CreateUserCommand(fullName, email, null, role, Password), ct);
            if (result.IsFailure) throw new InvalidOperationException($"Seeding {email} failed: {result.Error.Message}");
        }

        await updateSettings.Handle(
            new UpdateSettingsCommand("FieldOps Services LLC", "Dubai, United Arab Emirates", null, 5.00m, 150m, 30, "AED"), ct);

        logger.LogInformation("Seeded {Count} development users", Users.Length);
    }
}
