using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UltraSol.Modules.Auth.Application.Features.Accounts.Commands;
using UltraSol.Modules.Catalog.Application.Features.Products.Commands;
using UltraSol.Modules.Organization.Application.Features.Employees.Commands;
using UltraSol.Shared.Application.Caching;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class ModuleRegistrationTests
{
    [Fact]
    public void AllFeatureHandlersResolveThroughModuleRegistration()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ApplicationName = typeof(ModuleRegistrationTests).Assembly.FullName });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Postgres:ConnectionString"] = "Host=localhost;Database=registration_only;Username=unused",
            ["Auth:SigningKey"] = new string('x', 64)
        });
        builder.Services.AddSingleton<ICacheService, UnusedCache>();
        var assemblies = new[] { typeof(LoginCommand).Assembly, typeof(CreateEmployeeCommand).Assembly, typeof(CreateProductCommand).Assembly };
        foreach (var module in new[] { "Catalog", "Organization", "Auth" })
        {
            var api = Assembly.Load($"UltraSol.Modules.{module}.Api");
            var registration = api.GetType($"UltraSol.Modules.{module}.Api.{module}Module")!.GetMethod($"Add{module}Module")!;
            registration.Invoke(null, [builder.Services, builder.Configuration]);
        }
        using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        foreach (var request in assemblies.SelectMany(assembly => assembly.GetTypes()).Where(type => !type.IsAbstract && !type.IsGenericTypeDefinition))
        {
            var contract = request.GetInterfaces().SingleOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequest<>));
            if (contract is not null)
            {
                Assert.NotNull(scope.ServiceProvider.GetRequiredService(typeof(IRequestHandler<,>).MakeGenericType(request, contract.GenericTypeArguments[0])));
            }
        }
    }

    private sealed class UnusedCache : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Registration must not read cache.");
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Registration must not write cache.");
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}