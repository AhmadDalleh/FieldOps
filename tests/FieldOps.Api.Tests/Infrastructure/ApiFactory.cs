using System.Net.Http.Headers;
using System.Net.Http.Json;
using FieldOps.Application.Abstractions;
using FieldOps.Application.Features.Auth;
using FieldOps.Application.Features.Users;
using FieldOps.Domain.Identity;
using FieldOps.Domain.Settings;
using FieldOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace FieldOps.Api.Tests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Pass123!";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private Respawner _respawner = null!;

    public InMemoryFileStorage Files { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _container.GetConnectionString());
        builder.UseSetting("Jwt:Key", "test-signing-key-that-is-at-least-32-bytes-long");
        builder.ConfigureTestServices(services => services.AddSingleton<IFileStorage>(Files));
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

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

    public async Task ResetAsync()
    {
        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await _respawner.ResetAsync(connection);
        }

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AppSettings.Add(AppSettings.CreateDefault());
        await db.SaveChangesAsync();
    }

    public async Task<UserDto> CreateUserAsync(Role role, string? email = null)
    {
        email ??= $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@fieldops.test";
        await using var scope = Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CreateUserHandler>()
            .Handle(new CreateUserCommand($"{role} User", email, null, role, Password), default);
        return result.Value;
    }

    /// <summary>Returns a client carrying a real JWT obtained through the login endpoint.</summary>
    public async Task<HttpClient> CreateClientAs(Role role) => await LoginAs(await CreateUserAsync(role));

    /// <summary>Returns a client signed in as an existing user.</summary>
    public async Task<HttpClient> LoginAs(UserDto user)
    {
        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = Password });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(Json.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}

[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected ApiFactory Factory => factory;

    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
