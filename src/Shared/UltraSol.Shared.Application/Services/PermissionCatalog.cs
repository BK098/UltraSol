using System.Reflection;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Messaging.Quries;

namespace UltraSol.Shared.Application.Services
{
    public sealed class PermissionCatalog(IEnumerable<Assembly> assemblies)
    {
        public IReadOnlyDictionary<Type, string> Requests { get; } = Discover(assemblies);

        public static string GetPermission(Type requestType)
        {
            var parts = (requestType.Namespace ?? "").Split('.');
            var moduleIndex = Array.IndexOf(parts, "Modules");
            var featureIndex = Array.IndexOf(parts, "Features");
            if (moduleIndex < 0 || moduleIndex + 1 >= parts.Length || featureIndex < 0 || featureIndex + 1 >= parts.Length)
            {
                throw new InvalidOperationException($"Request {requestType.FullName} needs a Modules.<Module>.Application.Features.<Feature> namespace.");
            }
            var name = requestType.Name;
            foreach (var suffix in new[] { "Command", "Query" })
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    name = name[..^suffix.Length];
                }
            }
            return $"{parts[moduleIndex + 1]}.{parts[featureIndex + 1]}.{name}";
        }

        private static Dictionary<Type, string> Discover(IEnumerable<Assembly> assemblies)
        {
            var requests = assemblies.Distinct().SelectMany(assembly => assembly.GetTypes())
                .Where(type => !type.IsAbstract && !type.ContainsGenericParameters && type.GetInterfaces().Any(contract =>
                    contract == typeof(ICommand) || contract.IsGenericType &&
                    (contract.GetGenericTypeDefinition() == typeof(ICommand<>) || contract.GetGenericTypeDefinition() == typeof(IQuery<>))))
                .ToDictionary(type => type, GetPermission);
            var duplicate = requests.GroupBy(pair => pair.Value).FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null)
            {
                throw new InvalidOperationException($"Duplicate permission: {duplicate.Key}");
            }
            return requests;
        }
    }
}