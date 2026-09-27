using System.Text;
using FieldOps.Application.Abstractions;
using FieldOps.Application.Features.Notifications;
using FieldOps.Infrastructure.Email;
using FieldOps.Infrastructure.Files;
using FieldOps.Infrastructure.Identity;
using FieldOps.Infrastructure.Pdf;
using FieldOps.Infrastructure.Persistence;
using FieldOps.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FieldOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<AuditInterceptor>();
        services.AddScoped<NotificationInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(configuration.GetConnectionString("Default"))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>(), sp.GetRequiredService<NotificationInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => Encoding.UTF8.GetByteCount(o.Key ?? "") >= 32, "Jwt:Key must be at least 32 bytes.")
            .ValidateOnStart();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<INumberSequence, NumberSequenceService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IInvoicePdfRenderer, InvoicePdfGenerator>();
        services.AddSingleton(configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>() ?? new SmtpOptions());
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton(configuration.GetSection(NotificationOptions.SectionName).Get<NotificationOptions>() ?? new NotificationOptions());
        services.AddScoped<DevSeeder>();
        services.AddScoped<DemoDataSeeder>();

        return services;
    }

    public static TokenValidationParameters TokenValidationParameters(JwtOptions options) => new()
    {
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
        NameClaimType = ClaimNames.Name,
        RoleClaimType = ClaimNames.Role,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
}
