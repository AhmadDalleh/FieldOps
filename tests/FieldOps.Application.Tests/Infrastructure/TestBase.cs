using FieldOps.Application.Features.Auth;
using FieldOps.Application.Features.Users;
using FieldOps.Domain.Identity;
using FieldOps.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FieldOps.Application.Tests.Infrastructure;

[Collection(PostgresCollection.Name)]
public abstract class TestBase(PostgresFixture fixture) : IAsyncLifetime
{
    protected const string Password = "Pass123!";

    private readonly List<AsyncServiceScope> _scopes = [];

    protected PostgresFixture Fixture => fixture;

    public Task InitializeAsync() => fixture.ResetAsync();

    public async Task DisposeAsync()
    {
        foreach (var scope in _scopes) await scope.DisposeAsync();
    }

    /// <summary>Resolves a service from a fresh scope, like a new HTTP request would.</summary>
    protected T Resolve<T>() where T : notnull
    {
        var scope = fixture.Services.CreateAsyncScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    protected AppDbContext NewDb() => Resolve<AppDbContext>();

    protected async Task<UserDto> GivenUser(Role role, string? email = null, string password = Password)
    {
        email ??= $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@fieldops.test";
        var result = await Resolve<CreateUserHandler>()
            .Handle(new CreateUserCommand($"{role} User", email, "+971500000000", role, password), default);
        return result.Value;
    }

    protected Task<Domain.Common.Result<AuthResponse>> Login(string email, string password = Password) =>
        Resolve<LoginHandler>().Handle(new LoginCommand(email, password), default);
}
