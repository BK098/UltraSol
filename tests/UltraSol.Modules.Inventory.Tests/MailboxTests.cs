using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UltraSol.Modules.Inventory.Api;
using UltraSol.Modules.Inventory.Application;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Domain.Repositories;
using UltraSol.Modules.Inventory.Infrastructure;
using UltraSol.Modules.Inventory.Infrastructure.Persistence;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.Infrastructure.Messaging;
using Xunit;

namespace UltraSol.Modules.Inventory.Tests;

public sealed class MailboxTests(InventoryDatabaseFixture fixture) : IClassFixture<InventoryDatabaseFixture>
{
    [InventoryPostgresFact]
    public async Task TestInboxRollsBackFailureAndDeduplicatesSuccessfulRelease()
    {
        await using var services = fixture.Services(configure: collection => collection.AddScoped<IIntegrationHandler<CancelFixtureEvent>, CancelFixtureHandler>());
        var module = new InventoryModuleService(services.GetRequiredService<IServiceScopeFactory>());
        var item = Guid.NewGuid();
        await module.ReceiveAsync(new(Guid.NewGuid(), item, 4, "Receipt", Guid.NewGuid(), null), "actor");
        var reservation = await module.ReserveAsync(new(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1), [new(item, 2)]), "actor");
        var message = new CancelFixtureEvent(Guid.NewGuid(), Guid.NewGuid(), 1, 1, reservation.OrderId, true);
        await using (var failed = services.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => failed.ServiceProvider.GetRequiredService<Mailbox<InventoryDbContext>>()
                .ReceiveAsync(typeof(CancelFixtureEvent), JsonSerializer.Serialize(message), default));
        }
        await using (var db = fixture.Create())
        {
            Assert.False(await db.Set<InboxMessage>().AnyAsync(value => value.Id == message.EventId));
        }
        Assert.Equal(2, (await module.GetAvailabilityAsync([item]))[0].Reserved);
        message = message with { FailAfterMutation = false };
        await using (var successful = services.CreateAsyncScope())
        {
            var mailbox = successful.ServiceProvider.GetRequiredService<Mailbox<InventoryDbContext>>();
            await mailbox.ReceiveAsync(typeof(CancelFixtureEvent), JsonSerializer.Serialize(message), default);
        }
        string stamp;
        await using (var db = fixture.Create())
        {
            stamp = (await db.Reservations.SingleAsync(value => value.Id == reservation.Id)).ConcurrencyStamp;
        }
        await using (var replay = services.CreateAsyncScope())
        {
            await replay.ServiceProvider.GetRequiredService<Mailbox<InventoryDbContext>>().ReceiveAsync(typeof(CancelFixtureEvent), JsonSerializer.Serialize(message), default);
        }
        await using var verify = fixture.Create();
        Assert.Equal(stamp, (await verify.Reservations.SingleAsync(value => value.Id == reservation.Id)).ConcurrencyStamp);
        Assert.Equal(0, (await module.GetAvailabilityAsync([item]))[0].Reserved);
        Assert.Equal(1, await verify.Set<InboxMessage>().CountAsync(value => value.Id == message.EventId));
        Assert.Equal(1, await verify.Movements.CountAsync(value => value.ProductItemId == item));
    }

    [InventoryPostgresFact]
    public async Task TestOutboxCommitsWithStockAndLedgerAndRollsBackTogether()
    {
        await using var services = fixture.Services();
        var request = new ReceiveStockRequest(Guid.NewGuid(), Guid.NewGuid(), 5, "Receipt", Guid.NewGuid(), null);
        var message = new StockFixtureEvent(Guid.NewGuid(), Guid.NewGuid(), 1, 1, request.ProductItemId);
        await using (var failed = services.CreateAsyncScope())
        {
            var operations = failed.ServiceProvider.GetRequiredService<InventoryOperations>();
            var mailbox = failed.ServiceProvider.GetRequiredService<Mailbox<InventoryDbContext>>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => failed.ServiceProvider.GetRequiredService<IInventoryUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await operations.ReceiveAsync(request, "test", ct);
                mailbox.Add(message);
                throw new InvalidOperationException("Injected test failure before commit.");
            }));
        }
        await using (var db = fixture.Create())
        {
            Assert.False(await db.Stocks.AnyAsync(value => value.ProductItemId == request.ProductItemId));
            Assert.False(await db.Movements.AnyAsync(value => value.Id == request.OperationId));
            Assert.False(await db.Set<OutboxMessage>().AnyAsync(value => value.Id == message.EventId));
        }
        await using (var committed = services.CreateAsyncScope())
        {
            var operations = committed.ServiceProvider.GetRequiredService<InventoryOperations>();
            var mailbox = committed.ServiceProvider.GetRequiredService<Mailbox<InventoryDbContext>>();
            await committed.ServiceProvider.GetRequiredService<IInventoryUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await operations.ReceiveAsync(request, "test", ct);
                mailbox.Add(message);
            });
        }
        await using var verify = fixture.Create();
        Assert.Equal(5, (await verify.Stocks.SingleAsync(value => value.ProductItemId == request.ProductItemId)).OnHand);
        Assert.True(await verify.Movements.AnyAsync(value => value.Id == request.OperationId));
        Assert.Null((await verify.Set<OutboxMessage>().SingleAsync(value => value.Id == message.EventId)).PublishedAt);
        var registration = Assert.Single(services.GetServices<ModuleMailbox>());
        Assert.Equal("inventory", registration.Name);
        Assert.Empty(registration.Subscriptions);
    }

    [Fact]
    public void InventoryRegistersExpiryWorkerAndPublisherMailbox()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Postgres:ConnectionString"] = "Host=localhost;Database=registration_only;Username=unused"
        }).Build();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInventoryModule(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Contains(provider.GetServices<IHostedService>(), service => service is ReservationExpiryWorker);
        var registration = Assert.Single(provider.GetServices<ModuleMailbox>());
        Assert.Equal("inventory", registration.Name);
        Assert.Empty(registration.Subscriptions);
    }

    public sealed record CancelFixtureEvent(Guid EventId, Guid CorrelationId, long AggregateVersion, int ContractVersion, Guid OrderId, bool FailAfterMutation);
    public sealed record StockFixtureEvent(Guid EventId, Guid CorrelationId, long AggregateVersion, int ContractVersion, Guid ProductItemId);

    public sealed class CancelFixtureHandler(InventoryOperations operations) : IIntegrationHandler<CancelFixtureEvent>
    {
        public async Task HandleAsync(CancelFixtureEvent message, CancellationToken ct)
        {
            await operations.ReleaseByOrderAsync(message.OrderId, "ordering-test", ct);
            if (message.FailAfterMutation)
            {
                throw new InvalidOperationException("Injected consumer failure.");
            }
        }
    }
}