using FieldOps.Domain.Inventory;
using FieldOps.Application;
using FieldOps.Application.Abstractions;
using FieldOps.Domain.Settings;
using FieldOps.Infrastructure;
using FieldOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace FieldOps.Application.Tests.Infrastructure;

/// <summary>One Postgres container per test assembly. Migrations run once; each test resets the data with Respawn.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private Respawner _respawner = null!;

    public static readonly DateTimeOffset Start = new(2026, 10, 1, 6, 0, 0, TimeSpan.Zero);

    public ServiceProvider Services { get; private set; } = null!;
    public FakeTimeProvider Clock { get; private set; } = null!;
    public TestCurrentUser CurrentUser { get; private set; } = null!;
    public InMemoryFileStorage Files { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        BuildServices();

        await using (var scope = Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory", "roles"],
        });
    }

    /// <summary>Every test gets a fresh container with its own clock, signed-out user and empty file store.</summary>
    private void BuildServices()
    {
        Clock = new FakeTimeProvider(Start);
        CurrentUser = new TestCurrentUser();
        Files = new InMemoryFileStorage();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _container.GetConnectionString(),
                ["Jwt:Key"] = "test-signing-key-that-is-at-least-32-bytes-long",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<ICurrentUser>(CurrentUser);
        services.AddSingleton<IFileStorage>(Files);
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public async Task ResetAsync()
    {
        await Services.DisposeAsync();
        BuildServices();

        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AppSettings.Add(AppSettings.CreateDefault());
        db.StockLocations.Add(StockLocation.CreateMainWarehouse());
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
