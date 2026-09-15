using MediatR;
using UltraSol.Modules.Auth.Application.Features.Accounts.Commands;
using UltraSol.Modules.Catalog.Application.Features.Products.Commands;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Modules.Organization.Application.Features.Employees.Commands;
using UltraSol.Modules.Organization.Domain.Employees;
using UltraSol.Modules.Organization.Infrastructure.Persistence;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Messaging.Quries;
using UltraSol.Shared.Application.Services;
using UltraSol.Shared.IntegrationEvents.Organization;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class ModuleArchitectureTests
{
    [Fact]
    public void ModulesDoNotReferenceOtherModules()
    {
        foreach (var assembly in new[] { typeof(CreateEmployeeCommand).Assembly, typeof(OrganizationDbContext).Assembly, typeof(CatalogDbContext).Assembly })
        {
            var module = assembly.GetName().Name!.Split('.')[2];
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name!.StartsWith("UltraSol.Modules.") &&
                reference.Name.Split('.')[2] != module);
        }
    }

    [Fact]
    public void EveryAuthAndOrganizationFeatureHasItsOwnHandlerAndCorrectContract()
    {
        foreach (var assembly in new[] { typeof(LoginCommand).Assembly, typeof(CreateEmployeeCommand).Assembly })
        {
            var types = assembly.GetTypes();
            foreach (var request in types.Where(type => type.IsClass && !type.IsAbstract && (type.Name.EndsWith("Command") || type.Name.EndsWith("Query"))))
            {
                var contract = request.Name.EndsWith("Query") ? typeof(IQuery<>) : typeof(ICommand<>);
                Assert.Contains(request.GetInterfaces(), value => value.IsGenericType && value.GetGenericTypeDefinition() == contract);
                Assert.Contains(types, type => !type.IsGenericTypeDefinition && type.GetInterfaces().Any(value => value.IsGenericType && value.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) && value.GenericTypeArguments[0] == request));
                Assert.Contains(request.Name.EndsWith("Query") ? ".Queries" : ".Commands", request.Namespace!);
            }
            Assert.DoesNotContain(types, type => type.Name is "AuthOperations" or "OrganizationOperations" or "SecurityAdministration");
        }
    }

    [Fact]
    public void PermissionNamesAreStableAndEmployeeFeaturesHaveTheirOwnGroup()
    {
        var catalog = new PermissionCatalog([typeof(LoginCommand).Assembly, typeof(CreateEmployeeCommand).Assembly, typeof(CreateProductCommand).Assembly]);
        Assert.Equal("Auth.Accounts.Login", catalog.Requests[typeof(LoginCommand)]);
        Assert.Equal("Organization.Employees.CreateEmployee", catalog.Requests[typeof(CreateEmployeeCommand)]);
        Assert.Equal(catalog.Requests.Count, catalog.Requests.Values.Distinct().Count());
    }

    [Fact]
    public void IntegrationContractsContainNoCredentialsOrDomainEntities()
    {
        foreach (var contract in typeof(EmployeeAccountRequested).Assembly.GetTypes().Where(type => type.IsPublic))
        {
            Assert.NotNull(contract.GetProperty("EventId"));
            Assert.NotNull(contract.GetProperty("CorrelationId"));
            Assert.NotNull(contract.GetProperty("AggregateVersion"));
            Assert.NotNull(contract.GetProperty("ContractVersion"));
            Assert.DoesNotContain(contract.GetProperties(), property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(contract.GetProperties(), property => property.PropertyType.Namespace?.Contains(".Domain") == true);
        }
    }

    [Fact]
    public void EmployeeRejectsStaleResponsesAndRestoresRejectedAccessChange()
    {
        var employee = new Employee("employee@example.com", Guid.NewGuid());
        var provisioning = employee.CorrelationId;
        employee.Complete(Guid.NewGuid(), 1, Guid.NewGuid(), null);
        Assert.Equal("Pending", employee.SyncStatus);
        employee.Complete(provisioning, 1, Guid.NewGuid(), null);
        Assert.Equal("Completed", employee.SyncStatus);
        employee.Update(employee.DepartmentId, false);
        Assert.False(employee.IsActive);
        employee.Complete(provisioning, 1, employee.UserId, null);
        Assert.Equal("Pending", employee.SyncStatus);
        employee.Complete(employee.CorrelationId, employee.Version, employee.UserId, "Last System");
        Assert.True(employee.IsActive);
        Assert.Equal("Rejected", employee.SyncStatus);
        Assert.Equal("Last System", employee.SyncError);
    }
}