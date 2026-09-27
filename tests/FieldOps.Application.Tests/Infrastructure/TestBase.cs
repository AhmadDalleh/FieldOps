using FieldOps.Application.Features.Auth;
using FieldOps.Application.Features.Customers;
using FieldOps.Application.Features.Sites;
using FieldOps.Application.Features.Users;
using FieldOps.Application.Features.WorkOrders;
using FieldOps.Domain.Customers;
using FieldOps.Domain.WorkOrders;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>Signs in a new dispatcher, the usual author of work orders.</summary>
    protected async Task<UserDto> SignInOffice(Role role = Role.Dispatcher)
    {
        var user = await GivenUser(role);
        fixture.CurrentUser.SignInAs(user.Id, role);
        return user;
    }

    /// <summary>Creates a technician user and returns its user and technician ids.</summary>
    protected async Task<(Guid UserId, Guid TechnicianId)> GivenTechnician()
    {
        var user = await GivenUser(Role.Technician);
        var technicianId = await NewDb().Technicians.Where(t => t.UserId == user.Id).Select(t => t.Id).SingleAsync();
        return (user.Id, technicianId);
    }

    protected async Task<(Guid CustomerId, Guid SiteId)> GivenCustomerAndSite(string name = "Acme")
    {
        var customer = (await Resolve<CreateCustomerHandler>().Handle(new CreateCustomerCommand(
            new CustomerInput(name, CustomerType.Business, null, "+971500000000", null, null, null), Force: true), default)).Value;
        var site = (await Resolve<AddSiteHandler>().Handle(new AddSiteCommand(customer.Id,
            new SiteInput("HQ", "Sheikh Zayed Road", null, "Dubai", null, null, 25.2, 55.27, "Gate 4411")), default)).Value;
        return (customer.Id, site.Id);
    }

    /// <summary>Creates a work order as the signed-in user (signing in a dispatcher first when nobody is).</summary>
    protected async Task<WorkOrderDto> GivenWorkOrder(
        Guid customerId, Guid siteId, Guid? assetId = null, WorkOrderType type = WorkOrderType.Repair,
        WorkOrderPriority priority = WorkOrderPriority.Medium, string title = "AC not cooling", DateTimeOffset? dueBy = null)
    {
        if (!fixture.CurrentUser.IsAuthenticated) await SignInOffice();
        var result = await Resolve<CreateWorkOrderHandler>().Handle(new CreateWorkOrderCommand(
            new CreateWorkOrderInput(customerId, siteId, assetId, title, null, type, priority, dueBy)), default);
        return result.Value;
    }

    /// <summary>Moves a work order through the state machine directly, as later phases' endpoints will.</summary>
    protected async Task Advance(Guid workOrderId, Func<WorkOrder, Guid, DateTimeOffset, Domain.Common.Result> step)
    {
        var db = NewDb();
        var workOrder = await db.WorkOrders.Include(w => w.Tasks).Include(w => w.TimeEntries).SingleAsync(w => w.Id == workOrderId);
        var userId = fixture.CurrentUser.IsAuthenticated ? fixture.CurrentUser.UserId : (await GivenUser(Role.Admin)).Id;
        var result = step(workOrder, userId, fixture.Clock.GetUtcNow());
        if (result.IsFailure) throw new InvalidOperationException(result.Error.Message);
        await db.SaveChangesAsync();
    }

    protected Task Assign(Guid workOrderId, Guid technicianId) =>
        Advance(workOrderId, (w, u, now) => w.Schedule(technicianId, now.AddHours(1), now.AddHours(3), u, now));

    /// <summary>Schedules, dispatches, starts and completes the work order for the technician.</summary>
    protected async Task Complete(Guid workOrderId, Guid technicianId, string notes = "Replaced capacitor")
    {
        await Assign(workOrderId, technicianId);
        await Advance(workOrderId, (w, u, now) => w.Dispatch(u, now));
        await Advance(workOrderId, (w, u, now) => w.Start(u, now));
        var signature = await GivenSignature(workOrderId);
        await Advance(workOrderId, (w, u, now) => w.Complete(notes, "Sara M.", signature, u, now, "Not needed"));
    }

    /// <summary>Stores a signature attachment on an in-progress work order and returns its id.</summary>
    protected async Task<Guid> GivenSignature(Guid workOrderId)
    {
        var db = NewDb();
        var workOrder = await db.WorkOrders.AsNoTracking().SingleAsync(w => w.Id == workOrderId);
        var userId = fixture.CurrentUser.IsAuthenticated ? fixture.CurrentUser.UserId : (await GivenUser(Role.Admin)).Id;
        var signature = Attachment.Create(workOrder, AttachmentKind.Signature, "signature.png", "image/png", 100, userId,
            fixture.Clock.GetUtcNow()).Value;
        db.Attachments.Add(signature);
        await db.SaveChangesAsync();
        return signature.Id;
    }

    protected void SignInTechnician((Guid UserId, Guid TechnicianId) technician) =>
        fixture.CurrentUser.SignInAs(technician.UserId, Role.Technician, technician.TechnicianId);

    protected Task<Domain.Common.Result<AuthResponse>> Login(string email, string password = Password) =>
        Resolve<LoginHandler>().Handle(new LoginCommand(email, password), default);
}
