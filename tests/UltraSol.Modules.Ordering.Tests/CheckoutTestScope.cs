using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UltraSol.Modules.Ordering.Api;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Shared.Application.Authentication;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Modules.Ordering.Tests;

internal sealed class CheckoutTestScope
{
    internal static ServiceProvider Services(OrderingDatabaseFixture fixture, CheckoutDependencies dependencies, OrderingTestClock clock)
    {
        using var db = fixture.Create();
        var connection = db.Database.GetConnectionString();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Postgres:ConnectionString"] = string.IsNullOrWhiteSpace(connection) ? "Host=localhost;Database=ordering_model_only;Username=unused" : connection
        }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        services.AddOrderingModule(configuration);
        services.AddScoped<ICurrentAccount, OrderingTestAccount>();
        services.AddSingleton<IDomainEventDispatcher, OrderingTestDispatcher>();
        services.AddSingleton<ICatalogCheckoutClient>(dependencies);
        services.AddSingleton<IPricingQuoteClient>(dependencies);
        services.AddSingleton<IInventoryReservationClient>(dependencies);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    internal static AddressSnapshot Address => new("Buyer", "0900000000", "Street", null, null, null, null, null, "VN");
    internal static CheckoutCartRequest Request(string stamp) => new(stamp, new("Buyer", "buyer@example.com", "0900000000"), Address, null, new("COD", null));

    internal static async Task<CartService.CartResponse> Cart(IServiceProvider services, Guid item, int quantity = 2)
    {
        using var scope = services.CreateScope();
        var carts = scope.ServiceProvider.GetRequiredService<CartService>();
        var created = (await carts.CreateAsync("VND", default)).Data!;
        var changed = await carts.MutateAsync(created.Id, created.ConcurrencyStamp, created.GuestAccessToken,
            (cart, now) => cart.AddItem(item, quantity, now), default);
        return changed.Data! with { GuestAccessToken = created.GuestAccessToken };
    }
}

internal sealed class OrderingTestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class OrderingTestAccount : ICurrentAccount
{
    public Guid? UserId { get; set; }
    public Guid? SessionId => null;
}

internal sealed class OrderingTestDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class CheckoutDependencies(OrderingTestClock clock) : ICatalogCheckoutClient, IPricingQuoteClient, IInventoryReservationClient
{
    public readonly ConcurrentDictionary<Guid, Reservation> Reservations = new();
    public readonly List<ReserveRequest> ReservePayloads = [];
    public readonly Guid WarehouseId = Guid.NewGuid();
    public decimal Price { get; set; } = 120m;
    public string ProductName { get; set; } = "Original product";
    public int QuoteCalls;
    public bool LoseReserveResponse;
    public Action? BeforeCatalog;
    public Action? BeforeFind;
    public Action? AfterRelease;
    public Func<Task>? BeforeReserve { get; set; }
    public Func<Task>? AfterReserve;
    public CatalogItem[]? Items;

    public Task<CatalogItem[]> GetAsync(Guid[] productItemIds, CancellationToken ct)
    {
        BeforeCatalog?.Invoke();
        return Task.FromResult(Items ?? productItemIds
            .Select(id => new CatalogItem(id, Guid.NewGuid(), "SKU-1", ProductName, "Black", null, true, null, false, [])).ToArray());
    }

    public Task<PriceQuote> QuoteAsync(QuoteRequest request, CancellationToken ct)
    {
        Interlocked.Increment(ref QuoteCalls);
        var lines = request.Lines.Select(line => new QuoteLine(line.ProductItemId, line.Quantity, Price, Price, 0, Price * line.Quantity,
            "PriceList", Guid.NewGuid(), null, null, null, line.NegotiatedPriceId, request.ContractId)).ToArray();
        return Task.FromResult(new PriceQuote(Guid.NewGuid(), clock.Now, request.Currency, lines, lines.Sum(line => line.LineTotal), 0, 0, 0, lines.Sum(line => line.LineTotal)));
    }

    public async Task<Reservation> ReserveAsync(ReserveRequest request, CancellationToken ct)
    {
        if (BeforeReserve is not null)
        {
            await BeforeReserve();
        }
        lock (ReservePayloads)
        {
            ReservePayloads.Add(request);
        }
        var reservation = Reservations.GetOrAdd(request.OrderId, _ => new Reservation(Guid.NewGuid(), request.OrderId, "Active", request.ExpiresAt,
            request.Lines.Select(line => new ReservationLine(WarehouseId, line.ProductItemId, line.Quantity)).ToArray()));
        if (AfterReserve is not null)
        {
            await AfterReserve();
        }
        if (LoseReserveResponse)
        {
            LoseReserveResponse = false;
            throw new OrderingFailure(503, "DependencyUnavailable", "Response lost.");
        }
        return reservation;
    }

    public Task<Reservation?> FindAsync(Guid orderId, CancellationToken ct)
    {
        BeforeFind?.Invoke();
        return Task.FromResult(Reservations.GetValueOrDefault(orderId));
    }

    public Task<Reservation> ReleaseAsync(Guid orderId, CancellationToken ct)
    {
        var result = Reservations.AddOrUpdate(orderId, _ => throw new OrderingFailure(404, "Missing", "Missing"), (_, value) =>
            value.Status == "Consumed" ? value : value with { Status = clock.Now >= value.ExpiresAt ? "Expired" : "Released" });
        AfterRelease?.Invoke();
        return Task.FromResult(result);
    }
}