using System.Text.Json.Serialization;
using FieldOps.Api.Common;
using FieldOps.Api.Endpoints;
using FieldOps.Api.Hubs;
using FieldOps.Application;
using FieldOps.Application.Abstractions;
using FieldOps.Infrastructure;
using FieldOps.Infrastructure.Identity;
using FieldOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = FieldOps.Infrastructure.DependencyInjection.TokenValidationParameters(jwt.Value);
        // Browsers cannot set headers on WebSockets, so the hub takes the token from the query string.
        bearer.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(NotificationsHub.Path))
                    context.Token = token;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorizationBuilder().AddFieldOpsPolicies();

builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<INotifier, SignalRNotifier>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader()
    .AllowAnyMethod()));
builder.Services.AddHealthChecks().AddNpgSql(builder.Configuration.GetConnectionString("Default")!);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DevSeeder>().SeedAsync(CancellationToken.None);
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapSettingsEndpoints();
app.MapCustomerEndpoints();
app.MapSiteEndpoints();
app.MapAssetEndpoints();
app.MapTechnicianEndpoints();
app.MapWorkOrderEndpoints();
app.MapDispatchEndpoints();
app.MapMeEndpoints();
app.MapInventoryEndpoints();
app.MapInvoiceEndpoints();
app.MapNotificationEndpoints();
app.MapHub<NotificationsHub>(NotificationsHub.Path);

app.Run();

public partial class Program;
