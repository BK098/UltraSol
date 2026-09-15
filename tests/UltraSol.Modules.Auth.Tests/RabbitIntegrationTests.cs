using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Modules.Organization.Application.Features.Departments.Commands;
using UltraSol.Modules.Organization.Application.Features.Employees.Commands;
using UltraSol.Modules.Organization.Application.Messaging;
using UltraSol.Modules.Organization.Domain.Departments;
using UltraSol.Modules.Organization.Domain.Employees;
using UltraSol.Modules.Organization.Domain.Repositories;
using UltraSol.Modules.Organization.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Infrastructure.ThirdParties.MessageQueues.RabbitMessageQueues;
using UltraSol.Shared.IntegrationEvents.Auth;
using UltraSol.Shared.IntegrationEvents.Organization;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class RabbitFactAttribute : FactAttribute
{
    public RabbitFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_AUTH_POSTGRES_TESTS") != "1" || Environment.GetEnvironmentVariable("ULTRASOL_AUTH_RABBITMQ_TESTS") != "1")
        {
            Skip = "Enable PostgreSQL and RabbitMQ integration tests explicitly.";
        }
    }
}

public sealed class RabbitIntegrationTests(AuthDatabaseFixture fixture) : IClassFixture<AuthDatabaseFixture>
{
    [RabbitFact]
    public async Task MassTransitDeliversPendingOutboxAndPoisonMessagesReachErrorQueueAfterRetries()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UltraSol.slnx")))
        {
            directory = directory.Parent;
        }
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(directory!.FullName,
            "src/Bootstrappers/UltraSol.Bootstrappers/appsettings.json")).Build();
        var prefix = "ultrasol.test." + Guid.NewGuid().ToString("N");
        config["RabbitMQ:Prefix"] = prefix;
        var options = config.GetSection("RabbitMQ").Get<RabbitOptions>()!;
        ModuleMailbox[] modules =
        [
            new("auth", sp => sp.GetRequiredService<Mailbox<AuthDbContext>>(), [typeof(EmployeeAccountRequested), typeof(EmployeeAccessChanged)]),
            new("organization", sp => sp.GetRequiredService<Mailbox<OrganizationDbContext>>(), [typeof(EmployeeAccountProvisioned), typeof(EmployeeAccessApplied)])
        ];
        await using var services = OrganizationIntegrationTests.Services(fixture.Connection, collection =>
        {
            collection.AddSingleton<IConfiguration>(config);
            collection.AddLogging();
            foreach (var module in modules)
            {
                collection.AddSingleton(module);
            }
            collection.AddRabbitMessageQueues();
        });
        var workers = services.GetServices<IHostedService>().ToArray();
        Guid employeeId;
        await using (var scope = services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            await sp.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync();
            var sender = sp.GetRequiredService<ISender>();
            var department = (Department)(await sender.Send(new CreateDepartmentCommand("Rabbit test"))).Data!;
            employeeId = ((Employee)(await sender.Send(new CreateEmployeeCommand(department.Id, "rabbit-test@example.com"))).Data!).Id;
        }
        try
        {
            await Task.Delay(700);
            await using (var scope = services.CreateAsyncScope())
            {
                Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Set<OutboxMessage>().CountAsync(x => x.PublishedAt == null));
                Assert.Empty(await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Users.ToListAsync());
            }
            foreach (var worker in workers)
            {
                await worker.StartAsync(default);
            }
            Guid? userId = null;
            await Eventually(async () =>
            {
                await using var scope = services.CreateAsyncScope();
                var employee = await scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Employees.SingleAsync(x => x.Id == employeeId);
                userId = employee.UserId;
                return employee.SyncStatus == "Completed";
            });
            var poisonId = Guid.NewGuid();
            await using (var scope = services.CreateAsyncScope())
            {
                var sp = scope.ServiceProvider;
                await sp.GetRequiredService<IOrganizationUnitOfWork>().ExecuteInTransactionAsync(ct =>
                {
                    sp.GetRequiredService<IOrganizationOutbox>().Add(new EmployeeAccessChanged(poisonId, Guid.NewGuid(), 2, 99, employeeId, userId!.Value, false));
                    return Task.CompletedTask;
                });
            }
            await using var connection = await new ConnectionFactory { HostName = options.HostName, Port = options.Port, UserName = options.UserName, Password = options.Password, VirtualHost = options.VirtualHost }.CreateConnectionAsync();
            await Eventually(async () =>
            {
                await using var channel = await connection.CreateChannelAsync();
                try
                {
                    return await channel.MessageCountAsync(prefix + ".auth_error") == 1;
                }
                catch (RabbitMQ.Client.Exceptions.OperationInterruptedException error) when (error.ShutdownReason?.ReplyCode == 404)
                {
                    return false;
                }
            });
            await using (var scope = services.CreateAsyncScope())
            {
                Assert.False(await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Set<InboxMessage>().AnyAsync(x => x.Id == poisonId));
            }
        }
        finally
        {
            foreach (var worker in workers.Reverse())
            {
                await worker.StopAsync(default);
            }
            await using var connection = await new ConnectionFactory { HostName = options.HostName, Port = options.Port, UserName = options.UserName, Password = options.Password, VirtualHost = options.VirtualHost }.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            foreach (var module in modules)
            {
                foreach (var suffix in new[] { "", "_error", "_skipped" })
                {
                    await channel.QueueDeleteAsync(prefix + "." + module.Name + suffix, false, false);
                }
            }
            foreach (var module in modules)
            {
                foreach (var suffix in new[] { "", "_error", "_skipped" })
                {
                    await channel.ExchangeDeleteAsync(prefix + "." + module.Name + suffix, false);
                }
            }
        }
    }

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(50);
        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "Integration did not complete before the deadline.");
            await Task.Delay(200);
        }
    }
}