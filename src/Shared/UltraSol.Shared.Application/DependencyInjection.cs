using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using UltraSol.Shared.Application.Behaviors;

namespace UltraSol.Shared.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, params Assembly[] applicationAssemblies)
    {
        var assemblies = applicationAssemblies.Append(typeof(DependencyInjection).Assembly).Distinct().ToArray();
        var hasValidationBehavior = services.Any(descriptor =>
            descriptor.ServiceType == typeof(IPipelineBehavior<,>) &&
            descriptor.ImplementationType == typeof(ValidationBehavior<,>));

        ValidatorOptions.Global.DisplayNameResolver = (_, memberInfo, _) => memberInfo?.Name;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblies(assemblies);
            if (!hasValidationBehavior)
            {
                configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            }
        });
        services.AddValidatorsFromAssemblies(assemblies, ServiceLifetime.Scoped, includeInternalTypes: true);

        return services;
    }
}
