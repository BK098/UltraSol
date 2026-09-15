using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Application.Messaging;
using UltraSol.Modules.Auth.Application.Persistence;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Modules.Auth.Domain.Sessions;
using UltraSol.Modules.Auth.Infrastructure;
using UltraSol.Modules.Auth.Infrastructure.Messaging;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Modules.Auth.Infrastructure.Repositories;
using UltraSol.Modules.Organization.Application.Features.Departments.Commands;
using UltraSol.Modules.Organization.Application.Features.Employees.Commands;
using UltraSol.Modules.Organization.Application.Messaging;
using UltraSol.Modules.Organization.Domain.Departments;
using UltraSol.Modules.Organization.Domain.Employees;
using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Modules.Organization.Infrastructure.Messaging;
using UltraSol.Modules.Organization.Infrastructure.Persistence;
using UltraSol.Modules.Organization.Infrastructure.Repositories;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.IntegrationEvents.Auth;
using UltraSol.Shared.IntegrationEvents.Organization;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class OrganizationIntegrationTests(AuthDatabaseFixture fixture) : IClassFixture<AuthDatabaseFixture>
{
    internal static ServiceProvider Services(string connection, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Postgres:ConnectionString"] = connection }).Build();
        var services = new ServiceCollection().AddAuthInfrastructure(configuration);
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<AuthorizationRules>();
        services.AddScoped<Mailbox<AuthDbContext>>(sp => new(sp.GetRequiredService<AuthDbContext>(), sp.GetRequiredService<IAuthUnitOfWork>(), sp));
        services.AddScoped<IAuthOutbox, AuthOutbox>();
        services.AddScoped<IIntegrationHandler<EmployeeAccountRequested>, EmployeeAccountRequestedHandler>();
        services.AddScoped<IIntegrationHandler<EmployeeAccessChanged>, EmployeeAccessChangedHandler>();
        services.AddDbContext<OrganizationDbContext>(options => options.UseNpgsql(connection, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "organization")));
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IOrganizationUnitOfWork, OrganizationUnitOfWork>();
        //services.AddScoped<DepartmentRules>();
        services.AddScoped<Mailbox<OrganizationDbContext>>(sp => new(sp.GetRequiredService<OrganizationDbContext>(), sp.GetRequiredService<IOrganizationUnitOfWork>(), sp));
        services.AddScoped<IOrganizationOutbox, OrganizationOutbox>();
        services.AddScoped<IIntegrationHandler<EmployeeAccountProvisioned>, EmployeeAccountProvisionedHandler>();
        services.AddScoped<IIntegrationHandler<EmployeeAccessApplied>, EmployeeAccessAppliedHandler>();
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(CreateEmployeeCommand).Assembly));
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [PostgresFact]
    public async Task EmployeeProvisioningIsDurableIdempotentAndRejectsLastSystemDisable()
    {
        await using var services = Services(fixture.Connection);
        Guid employeeId;
        Guid departmentId;
        await using (var scope = services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            await sp.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync();
            var sender = sp.GetRequiredService<ISender>();
            var department = (Department)(await sender.Send(new CreateDepartmentCommand("Integration department"))).Data!;
            departmentId = department.Id;
            var employee = (Employee)(await sender.Send(new CreateEmployeeCommand(department.Id, "employee@example.com"))).Data!;
            employeeId = employee.Id;
            Assert.Null(employee.UserId);
            Assert.Equal("Pending", employee.SyncStatus);
        }
        await Deliver<OrganizationDbContext, AuthDbContext, EmployeeAccountRequested>(services);
        await Deliver<AuthDbContext, OrganizationDbContext, EmployeeAccountProvisioned>(services, twice: true);
        Guid userId;
        await using (var scope = services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var employee = await sp.GetRequiredService<OrganizationDbContext>().Employees.SingleAsync(x => x.Id == employeeId);
            userId = employee.UserId!.Value;
            Assert.Equal("Completed", employee.SyncStatus);
            var users = sp.GetRequiredService<UserManager<UserAccount>>();
            var user = (await users.FindByIdAsync(userId.ToString()))!;
            Assert.True(await users.CheckPasswordAsync(user, "abc@123"));
            var auth = sp.GetRequiredService<AuthDbContext>();
            Assert.Single(await auth.Set<EmployeeAccount>().ToListAsync());
            await sp.GetRequiredService<IAuthUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                var role = new Role("System", "System");
                auth.Roles.Add(role);
                auth.RoleAssignments.Add(new RoleAssignment(Guid.NewGuid(), userId, role.Id));
                auth.Sessions.Add(Session.Create(userId, Device.Create("Test", null, null), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1)));
                return Task.CompletedTask;
            });
            await sp.GetRequiredService<ISender>().Send(new UpdateEmployeeCommand(employeeId, departmentId, false));
            Assert.False(employee.IsActive);
        }
        await Deliver<OrganizationDbContext, AuthDbContext, EmployeeAccessChanged>(services);
        await Deliver<AuthDbContext, OrganizationDbContext, EmployeeAccessApplied>(services);
        await using (var scope = services.CreateAsyncScope())
        {
            var employee = await scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Employees.SingleAsync(x => x.Id == employeeId);
            Assert.True(employee.IsActive);
            Assert.Equal("Rejected", employee.SyncStatus);
            Assert.Contains("System", employee.SyncError!);
            var auth = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            Assert.Null((await auth.Sessions.SingleAsync(x => x.UserId == userId)).RevokedAt);
        }
    }

    [PostgresFact]
    public async Task OrganizationRollbackAlsoRollsBackOutbox()
    {
        await using var services = Services(fixture.Connection);
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<OrganizationDbContext>();
        await db.Database.MigrateAsync();
        var count = await db.Set<OutboxMessage>().CountAsync();
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => sp.GetRequiredService<IOrganizationUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            sp.GetRequiredService<IOrganizationOutbox>().Add(new EmployeeAccountRequested(id, id, 1, 1, id, "rollback@example.com", id));
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Simulated failure after save, before commit.");
        }));
        Assert.Equal(count, await db.Set<OutboxMessage>().CountAsync());
        Assert.False(await db.Set<OutboxMessage>().AnyAsync(x => x.Id == id));
    }

    private static async Task Deliver<TSource, TDestination, TContract>(ServiceProvider services, bool twice = false)
        where TSource : DbContext
        where TDestination : DbContext
    {
        OutboxMessage message;
        await using (var scope = services.CreateAsyncScope())
        {
            message = (await scope.ServiceProvider.GetRequiredService<Mailbox<TSource>>().NextAsync(default))!;
            Assert.Equal(typeof(TContract).FullName, message.Type);
            Assert.DoesNotContain("abc@123", message.Payload);
        }
        for (var attempt = 0; attempt < (twice ? 2 : 1); attempt++)
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<Mailbox<TDestination>>().ReceiveAsync(typeof(TContract), message.Payload, default);
        }
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Mailbox<TSource>>().MarkPublishedAsync(message.Id, default);
        }
    }


}