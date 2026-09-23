using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using UltraSol.Modules.Ordering.Application.Features.Checkout.Commands;
using UltraSol.Shared.Application.Services;
using Xunit;

namespace UltraSol.Modules.Ordering.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Ordering_production_assemblies_only_reference_their_own_module()
    {
        foreach (var layer in new[] { "Domain", "Application", "Infrastructure", "Api" })
        {
            var assembly = Assembly.Load("UltraSol.Modules.Ordering." + layer);
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name!.StartsWith("UltraSol.Modules.", StringComparison.Ordinal)
                && !reference.Name.StartsWith("UltraSol.Modules.Ordering.", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void All_ordering_requests_have_unique_permissions_and_resolvable_handlers()
    {
        var assembly = typeof(CheckoutCartCommand).Assembly;
        var catalog = new PermissionCatalog([assembly]);
        var requests = assembly.GetTypes().Where(type => type.GetInterfaces().Any(contract => contract.IsGenericType
            && contract.GetGenericTypeDefinition() == typeof(IRequest<>))).ToArray();
        Assert.Equal(requests.OrderBy(type => type.FullName), catalog.Requests.Keys.OrderBy(type => type.FullName));
        Assert.Contains(typeof(CheckoutCartCommand), catalog.Requests.Keys);
        Assert.All(catalog.Requests.Values, permission => Assert.StartsWith("Ordering.", permission));
        var clock = new OrderingTestClock();
        using var services = CheckoutTestScope.Services(new OrderingDatabaseFixture(), new(clock), clock);
        using var scope = services.CreateScope();
        foreach (var request in catalog.Requests.Keys)
        {
            var response = request.GetInterfaces().Single(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequest<>)).GenericTypeArguments[0];
            Assert.NotNull(scope.ServiceProvider.GetRequiredService(typeof(IRequestHandler<,>).MakeGenericType(request, response)));
        }
    }
}