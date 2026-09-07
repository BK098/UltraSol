//using Domain.Common.Repositories;
//using Microsoft.Extensions.DependencyInjection;
//using System.Reflection;

//namespace UltraSol.Shared.Infrastructure.DependencyInjections
//{
//    public static class RepositoryRegistration
//    {
//        private static readonly Type[] RepositoryDefinitions =
//        [
//            typeof(IRepository<>),
//            typeof(IRepository<,>)
//        ];

//        public static IServiceCollection AddRepositories(
//            this IServiceCollection services,
//            params Assembly[] assemblies)
//        {
//            var implementationTypes = assemblies
//                .SelectMany(GetLoadableTypes)
//                .Where(type =>
//                    type is { IsClass: true, IsAbstract: false } &&
//                    !type.ContainsGenericParameters &&
//                    IsRepositoryImplementation(type))
//                .ToArray();

//            foreach (var implementationType in implementationTypes)
//            {
//                var serviceTypes = implementationType
//                    .GetInterfaces()
//                    .Where(IsRepositoryContract)
//                    .Distinct()
//                    .ToArray();

//                foreach (var serviceType in serviceTypes)
//                {
//                    services.AddScoped(serviceType, implementationType);
//                }
//            }
//            return services;
//        }

//        private static bool IsRepositoryImplementation(Type type)
//        {
//            return type.GetInterfaces().Any(IsRepositoryContract);
//        }

//        private static bool IsRepositoryContract(Type interfaceType)
//        {
//            if (interfaceType.IsGenericType && RepositoryDefinitions.Contains(interfaceType.GetGenericTypeDefinition()))
//            {
//                return true;
//            }
//            return interfaceType.GetInterfaces().Any(IsRepositoryContract);
//        }

//        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
//        {
//            try
//            {
//                return assembly.GetTypes();
//            }
//            catch (ReflectionTypeLoadException exception)
//            {
//                return exception.Types.OfType<Type>();
//            }
//        }
//    }
//}