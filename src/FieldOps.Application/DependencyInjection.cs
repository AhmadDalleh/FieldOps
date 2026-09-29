using FieldOps.Application.Abstractions;
using FieldOps.Application.Features.Auth;
using FieldOps.Application.Features.Technicians;
using FieldOps.Application.Features.Users;
using FieldOps.Application.Features.WorkOrders;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FieldOps.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        var handlerInterfaces = new[] { typeof(ICommandHandler<,>), typeof(IQueryHandler<,>) };

        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var @interface in type.GetInterfaces()
                         .Where(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            {
                services.AddScoped(@interface, type);
                services.AddScoped(type);
            }
        }

        services.AddScoped<TokenIssuer>();
        services.AddScoped<TechnicianProvisioner>();
        services.AddScoped<TechnicianReader>();
        services.AddScoped<TimeOffReader>();
        services.AddScoped<WorkOrderReader>();
        services.AddScoped<NoteReader>();
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }
}
