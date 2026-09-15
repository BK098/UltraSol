using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using UltraSol.Shared.Application.Behaviors;
using UltraSol.Shared.Application.Services;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Shared.Infrastructure.DependencyInjections
{
    public static class RegistrationServicesExtensions
    {
        public static IServiceCollection AddRegistration(this IServiceCollection services, Assembly[] assemblies)
        {
            services.AddValidatorsFromAssemblies(assemblies, ServiceLifetime.Scoped, includeInternalTypes: true);

            services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblies(assemblies);
                configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            });

            #region Repository
            var implementationTypes = assemblies
                .SelectMany(GetLoadableTypes)
                .Where(type =>
                    type is { IsClass: true, IsAbstract: false } &&
                    !type.ContainsGenericParameters &&
                    IsRepositoryImplementation(type))
                .ToArray();

            foreach (var implementationType in implementationTypes)
            {
                var serviceTypes = implementationType.GetInterfaces().Where(IsRepositoryContract).Distinct().ToArray();
                foreach (var serviceType in serviceTypes)
                {
                    services.AddScoped(serviceType, implementationType);
                }
            }
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
            services.AddScoped(typeof(IQueryRepository<>), typeof(QueryRepository<>));
            services.AddScoped(typeof(ICommandRepository<>), typeof(CommandRepository<>));
            services.AddScoped(typeof(IQueryRepository<,>), typeof(QueryRepository<,>));
            services.AddScoped(typeof(ICommandRepository<,>), typeof(CommandRepository<,>));
            #endregion

            #region Permission CQRS
            services.AddSingleton(new PermissionCatalog(assemblies));

            #endregion
            return services;
        }
        private static readonly Type[] RepositoryDefinitions =
        [
            typeof(IRepository<>),
            typeof(IRepository<,>)
        ];
        private static bool IsRepositoryImplementation(Type type)
        {
            return type.GetInterfaces().Any(IsRepositoryContract);
        }
        private static bool IsRepositoryContract(Type interfaceType)
        {
            if (interfaceType.IsGenericType && RepositoryDefinitions.Contains(interfaceType.GetGenericTypeDefinition()))
            {
                return true;
            }
            return interfaceType.GetInterfaces().Any(IsRepositoryContract);
        }
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.OfType<Type>();
            }
        }
    }
}