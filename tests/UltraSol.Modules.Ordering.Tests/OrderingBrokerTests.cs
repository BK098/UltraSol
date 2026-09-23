using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Domain.Repositories;
using UltraSol.Modules.Inventory.Infrastructure.Messaging;
using UltraSol.Modules.Inventory.Infrastructure.Persistence;
using UltraSol.Modules.Inventory.Infrastructure.Repositories;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Messaging;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Domain.Repositories;
using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Modules.Ordering.Infrastructure.Checkout;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using UltraSol.Modules.Ordering.Infrastructure.Repositories;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Infrastructure.ThirdParties.MessageQueues.RabbitMessageQueues;
using UltraSol.Shared.IntegrationEvents.Inventory;
using Xunit;

namespace UltraSol.Modules.Ordering.Tests;

public sealed class OrderingRabbitFactAttribute : FactAttribute
{
    public OrderingRabbitFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_ORDERING_POSTGRES_TESTS") != "1" ||
            Environment.GetEnvironmentVariable("ULTRASOL_ORDERING_RABBITMQ_TESTS") != "1")
        {
            Skip = "Enable Ordering PostgreSQL and RabbitMQ integration tests explicitly.";
        }
    }
}

public sealed class OrderingBrokerTests(OrderingDatabaseFixture fixture) : IClassFixture<OrderingDatabaseFixture>
{
    [OrderingRabbitFact]
    public async Task InventoryExpiryOutboxSurvivesBrokerDowntimeThenCancelsConfirmedOrderOnce()
    {
        var root = RepositoryRoot();
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(root, "src/Bootstrappers/UltraSol.Bootstrappers/appsettings.json"))
            .Build();
        var prefix = "ultrasol.ordering.test." + Guid.NewGuid().ToString("N");
        configuration["RabbitMQ:Prefix"] = prefix;
        var rabbit = configuration.GetSection("RabbitMQ").Get<RabbitOptions>()!;
        var clock = new OrderingTestClock();
        var services = BuildServices(configuration, clock, prefix);
        var workers = services.GetServices<IHostedService>().ToArray();
        var started = new List<IHostedService>();
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var message = new InventoryReservationExpiredV1(Guid.NewGuid(), orderId, 1, clock.Now.AddMinutes(16),
            reservationId, orderId, clock.Now.AddMinutes(15));

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
            var store = scope.ServiceProvider.GetRequiredService<IOrderingStore>();
            await scope.ServiceProvider.GetRequiredService<IOrderingUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var order = Order.Place(orderId, await store.NextOrderNumberAsync(ct), null, "broker-owner", null, "hash",
                    OrderType.Retail, "Storefront", Buyer(), Address(), null, new PaymentTerm("COD", null), Guid.NewGuid(),
                    reservationId, clock.Now.AddMinutes(15), "VND", [Line()], clock.Now);
                order.Confirm(clock.Now);
                store.Add(order);
            });
            await scope.ServiceProvider.GetRequiredService<IInventoryUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                scope.ServiceProvider.GetRequiredService<IInventoryOutbox>().Add(message);
                return Task.CompletedTask;
            });
        }

        await using (var retained = services.CreateAsyncScope())
        {
            Assert.Equal(1, await retained.ServiceProvider.GetRequiredService<InventoryDbContext>().Set<OutboxMessage>()
                .CountAsync(value => value.Id == message.EventId && value.PublishedAt == null));
            Assert.False(await retained.ServiceProvider.GetRequiredService<OrderingDbContext>().Set<InboxMessage>()
                .AnyAsync(value => value.Id == message.EventId));
            Assert.Equal(OrderStatus.Confirmed, (await retained.ServiceProvider.GetRequiredService<IOrderingStore>()
                .OrderAsync(orderId, default))!.Status);
        }

        try
        {
            foreach (var worker in workers)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await worker.StartAsync(timeout.Token);
                started.Add(worker);
            }
            await Eventually(async () =>
            {
                await using var scope = services.CreateAsyncScope();
                var order = await scope.ServiceProvider.GetRequiredService<IOrderingStore>().OrderAsync(orderId, default);
                return order?.Status == OrderStatus.Cancelled;
            });

            var endpoint = await services.GetRequiredService<IBus>().GetSendEndpoint(new Uri("queue:" + prefix + ".ordering"));
            await endpoint.Send(message, context => context.MessageId = message.EventId);
            await endpoint.Send(message, context => context.MessageId = message.EventId);
            await Eventually(async () =>
            {
                await using var scope = services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Set<InboxMessage>()
                    .CountAsync(value => value.Id == message.EventId) == 1;
            });

            await using var verify = services.CreateAsyncScope();
            var order = (await verify.ServiceProvider.GetRequiredService<IOrderingStore>().OrderAsync(orderId, default))!;
            Assert.Equal(OrderStatus.Cancelled, order.Status);
            Assert.Equal(3, order.History.Count);
            Assert.Equal(1, await verify.ServiceProvider.GetRequiredService<OrderingDbContext>().Set<InboxMessage>()
                .CountAsync(value => value.Id == message.EventId));
            Assert.NotNull((await verify.ServiceProvider.GetRequiredService<InventoryDbContext>().Set<OutboxMessage>()
                .SingleAsync(value => value.Id == message.EventId)).PublishedAt);
        }
        finally
        {
            foreach (var worker in started.AsEnumerable().Reverse())
            {
                await worker.StopAsync(default);
            }
            await services.DisposeAsync();
            if (started.Count > 0)
            {
                await DeleteOwnedTopology(rabbit, prefix);
            }
        }
    }

    private ServiceProvider BuildServices(IConfiguration configuration, OrderingTestClock clock, string prefix)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<IDomainEventDispatcher, OrderingTestDispatcher>();
        services.AddDbContext<OrderingDbContext>(options => options.UseNpgsql(fixture.Connection,
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "ordering")));
        services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(fixture.Connection,
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "inventory")));
        services.AddScoped<IOrderingUnitOfWork, OrderingUnitOfWork>();
        services.AddScoped<IOrderingStore, OrderingStore>();
        services.AddScoped<IInventoryUnitOfWork, InventoryUnitOfWork>();
        services.AddScoped<Mailbox<OrderingDbContext>>(provider => new(provider.GetRequiredService<OrderingDbContext>(),
            provider.GetRequiredService<IOrderingUnitOfWork>(), provider));
        services.AddScoped<Mailbox<InventoryDbContext>>(provider => new(provider.GetRequiredService<InventoryDbContext>(),
            provider.GetRequiredService<IInventoryUnitOfWork>(), provider));
        services.AddScoped<IInventoryOutbox, InventoryOutbox>();
        services.AddScoped<IIntegrationHandler<InventoryReservationExpiredV1>, ReservationExpiredHandler>();
        services.AddSingleton(new ModuleMailbox("inventory", provider => provider.GetRequiredService<Mailbox<InventoryDbContext>>(), []));
        services.AddSingleton(new ModuleMailbox("ordering", provider => provider.GetRequiredService<Mailbox<OrderingDbContext>>(),
            [typeof(InventoryReservationExpiredV1)]));
        services.AddRabbitMessageQueues();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task DeleteOwnedTopology(RabbitOptions options, string prefix)
    {
        await using var connection = await new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost
        }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        foreach (var module in new[] { "inventory", "ordering" })
        {
            foreach (var suffix in new[] { "", "_error", "_skipped" })
            {
                await channel.QueueDeleteAsync(prefix + "." + module + suffix, false, false);
                await channel.ExchangeDeleteAsync(prefix + "." + module + suffix, false);
            }
        }
    }

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(40);
        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "Broker integration did not complete before the deadline.");
            await Task.Delay(200);
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UltraSol.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static BuyerSnapshot Buyer() => new("Guest", null, null, null, "Buyer", "buyer@example.com", "0900000000");
    private static AddressSnapshot Address() => new("Buyer", "0900000000", "Street", null, null, null, null, null, "VN");
    private static OrderLineSnapshot Line() => new(Guid.NewGuid(), Guid.NewGuid(), "Product", "SKU-1", "Black", null, 1,
        new PriceSnapshot(120m, 120m, 0m, "VND", "PriceList", Guid.NewGuid(), null, null, null, null, null));
}