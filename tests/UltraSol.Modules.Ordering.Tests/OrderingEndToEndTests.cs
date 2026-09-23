using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Modules.Catalog.Infrastructure.Repositories;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Infrastructure.Persistence;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using UltraSol.Modules.Pricing.Domain.Contracts;
using UltraSol.Modules.Pricing.Domain.Negotiations;
using UltraSol.Modules.Pricing.Domain.PriceLists;
using UltraSol.Modules.Pricing.Domain.Prices;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Domain.ValueObjects;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Modules.Pricing.Infrastructure.Repositories;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.IntegrationEvents.Inventory;
using Xunit;

namespace UltraSol.Modules.Ordering.Tests;

public sealed class OrderingEndToEndTests(OrderingDatabaseFixture fixture) : IClassFixture<OrderingDatabaseFixture>
{
    [OrderingPostgresFact]
    public async Task Guest_bundle_checkout_uses_real_HTTP_quotes_stock_and_cancellation_then_expiry_inbox()
    {
        using var ordering = fixture.Create();
        var clock = new OrderingTestClock();
        var connection = ordering.Database.GetConnectionString()!;
        await using var host = await StartRealModules(connection, clock);
        var product = Product.Create("Physical component");
        var item = ProductItem.Create(product, SKU.Create("ORD-COMPONENT-" + Guid.NewGuid().ToString("N")), []);
        item.Activate();
        product.Publish();
        var bundleProduct = Product.Create("Original bundle");
        var bundle = ProductItem.CreateBundle(bundleProduct, SKU.Create("ORD-BUNDLE-" + Guid.NewGuid().ToString("N")), [], [(item, 3)]);
        bundle.Activate();
        bundleProduct.Publish();
        Guid skuPriceId;
        using (var seed = host.App.Services.CreateScope())
        {
            var provider = seed.ServiceProvider;
            var catalog = provider.GetRequiredService<CatalogDbContext>();
            await new CatalogUnitOfWork(catalog, new OrderingTestDispatcher()).ExecuteInTransactionAsync(async ct =>
            {
                var products = new ProductRepository(catalog);
                var items = new ProductItemRepository(catalog);
                await products.AddAsync(product, ct);
                await products.AddAsync(bundleProduct, ct);
                await items.AddAsync(item, ct);
                await items.AddAsync(bundle, ct);
            });
            var pricing = provider.GetRequiredService<PricingDbContext>();
            var list = PriceList.Create("Ordering Retail", Currency.Create("VND"), PriceListType.Retail);
            var price = SkuPrice.Create(list, bundle.Id);
            price.SetInitialPrice(clock.Now, [PriceTier.Create(1, 120)], null, clock.Now);
            skuPriceId = price.Id;
            await new PricingUnitOfWork(pricing, new OrderingTestDispatcher()).ExecuteInTransactionAsync(async ct =>
            {
                await new PriceListRepository(pricing).AddAsync(list, ct);
                await new SkuPriceRepository(pricing).AddAsync(price, ct);
            });
            provider.GetRequiredService<IConfiguration>()["Pricing:DefaultPriceLists:Retail:VND"] = list.Id.ToString();
            await provider.GetRequiredService<IInventoryModule>().ReceiveAsync(new(Guid.NewGuid(), item.Id, 30, "Test", Guid.NewGuid(), null), null);
        }

        var client = host.Client;
        async Task<CartService.CartResponse> Cart()
        {
            client.DefaultRequestHeaders.Remove("X-Ordering-Guest-Token");
            var created = await OwnershipTests.Read<CartService.CartResponse>(await client.PostAsJsonAsync("api/ordering/carts", new { currency = "VND" }), 201);
            client.DefaultRequestHeaders.Add("X-Ordering-Guest-Token", created.GuestAccessToken);
            return await OwnershipTests.Read<CartService.CartResponse>(await client.PostAsJsonAsync($"api/ordering/carts/{created.Id}/items",
                new { created.ConcurrencyStamp, productItemId = bundle.Id, quantity = 2 }), 200);
        }
        var cart = await Cart();
        var preview = await OwnershipTests.Read<PriceQuote>(await client.PostAsync($"api/ordering/carts/{cart.Id}/quote", null), 200);
        Assert.Equal(240, preview.GrandTotal);
        using (var change = host.App.Services.CreateScope())
        {
            await change.ServiceProvider.GetRequiredService<IPricingUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var price = await change.ServiceProvider.GetRequiredService<ISkuPriceRepository>().GetTrackedRequiredAsync(skuPriceId, ct);
                price.SchedulePriceChange(clock.Now.AddMinutes(1), [PriceTier.Create(1, 150)], null, clock.Now);
            });
        }
        clock.Now = clock.Now.AddMinutes(2);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "real-http-checkout");
        var request = CheckoutTestScope.Request(cart.ConcurrencyStamp);
        var placed = await OwnershipTests.Read<CheckoutResult>(await client.PostAsJsonAsync($"api/ordering/carts/{cart.Id}/checkout", request), 201);
        Assert.Equal(300, placed.GrandTotal);
        var replay = await OwnershipTests.Read<CheckoutResult>(await client.PostAsJsonAsync($"api/ordering/carts/{cart.Id}/checkout", request), 200);
        Assert.Equal(placed.OrderId, replay.OrderId);
        using (var verify = host.App.Services.CreateScope())
        {
            var stock = Assert.Single(await verify.ServiceProvider.GetRequiredService<IInventoryModule>().GetAvailabilityAsync([item.Id]));
            Assert.Equal(6, stock.Reserved);
        }
        var cancel = await OwnershipTests.Read<OrderOperationResult>(await client.PostAsJsonAsync($"api/ordering/orders/{placed.OrderId}/cancel",
            new { placed.ConcurrencyStamp, reason = "Changed mind" }), 200);
        Assert.Equal("Completed", cancel.State);
        using (var verify = host.App.Services.CreateScope())
        {
            var stock = Assert.Single(await verify.ServiceProvider.GetRequiredService<IInventoryModule>().GetAvailabilityAsync([item.Id]));
            Assert.Equal((30, 0), (stock.Available, stock.Reserved));
        }
        var nextCart = await Cart();
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", "real-expiry");
        var next = await OwnershipTests.Read<CheckoutResult>(await client.PostAsJsonAsync($"api/ordering/carts/{nextCart.Id}/checkout",
            CheckoutTestScope.Request(nextCart.ConcurrencyStamp)), 201);
        using (var changeCatalog = host.App.Services.CreateScope())
        {
            var db = changeCatalog.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await new CatalogUnitOfWork(db, new OrderingTestDispatcher()).ExecuteInTransactionAsync(async ct =>
            {
                var changedProduct = await new ProductRepository(db).GetTrackedRequiredAsync(bundleProduct.Id, ct);
                changedProduct.Rename("Renamed after placement");
                changedProduct.Archive();
            });
        }
        // Historical reads must survive unavailable upstream services and current product changes.
        var configuration = host.App.Services.GetRequiredService<IConfiguration>();
        configuration["Ordering:CatalogApi:BaseUrl"] = "http://127.0.0.1:1";
        configuration["Ordering:PricingApi:BaseUrl"] = "http://127.0.0.1:1";
        clock.Now = next.ReservationExpiresAt!.Value.AddSeconds(1);
        using (var expire = host.App.Services.CreateScope())
        {
            await expire.ServiceProvider.GetRequiredService<IInventoryModule>().ReleaseByOrderAsync(next.OrderId!.Value, null);
            var db = expire.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var message = (await db.Set<OutboxMessage>().AsNoTracking().ToArrayAsync()).Select(value => JsonSerializer.Deserialize<InventoryReservationExpiredV1>(value.Payload)!)
                .Single(value => value.OrderId == next.OrderId);
            await LifecycleTests.Deliver(host.App.Services, message);
        }
        var expired = await OwnershipTests.Read<OrderReadService.OrderDetails>(await client.GetAsync($"api/ordering/orders/{next.OrderId}"), 200);
        Assert.Equal("Cancelled", expired.Status);
        Assert.Equal(300, expired.GrandTotal);
        Assert.Equal("Original bundle", expired.Lines.Single().ProductName);
    }

    [OrderingPostgresFact]
    public async Task Assisted_HTTP_requires_admin_and_preserves_Wholesale_Contract_Net_and_negotiated_context()
    {
        using var ordering = fixture.Create();
        var clock = new OrderingTestClock();
        await using var host = await StartRealModules(ordering.Database.GetConnectionString()!, clock);
        var product = Product.Create("Assisted item");
        var item = ProductItem.Create(product, SKU.Create("ORD-ASSISTED-" + Guid.NewGuid().ToString("N")), []);
        item.Activate();
        product.Publish();
        var customerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var wholesaleList = PriceList.Create("Ordering Wholesale", Currency.Create("VND"), PriceListType.Wholesale);
        var wholesalePrice = SkuPrice.Create(wholesaleList, item.Id);
        wholesalePrice.SetInitialPrice(clock.Now, [PriceTier.Create(1, 80)], null, clock.Now);
        var contractList = PriceList.Create("Ordering Contract", Currency.Create("VND"), PriceListType.Contract);
        var contract = Contract.Create(customerId, contractList, EffectivePeriod.Create(clock.Now, clock.Now.AddDays(30)), "Commercial terms independent of payment choice");
        var contractPrice = SkuPrice.Create(contractList, item.Id, contract);
        contractPrice.SetInitialPrice(clock.Now, [PriceTier.Create(1, 70)], null, clock.Now, contract);
        contract.Activate(null, clock.Now);
        var negotiationRef = Guid.NewGuid();
        var negotiated = NegotiatedPrice.Create(customerId, item.Id, negotiationRef, 2, 55, Currency.Create("VND"),
            PriceListType.Wholesale, null, "Agreed transaction", EffectivePeriod.Create(clock.Now, clock.Now.AddDays(1)), null, clock.Now);
        negotiated.Submit(null, clock.Now);
        negotiated.Approve(null, clock.Now);
        using (var seed = host.App.Services.CreateScope())
        {
            var provider = seed.ServiceProvider;
            var catalog = provider.GetRequiredService<CatalogDbContext>();
            await new CatalogUnitOfWork(catalog, new OrderingTestDispatcher()).ExecuteInTransactionAsync(async ct =>
            {
                await new ProductRepository(catalog).AddAsync(product, ct);
                await new ProductItemRepository(catalog).AddAsync(item, ct);
            });
            var pricing = provider.GetRequiredService<PricingDbContext>();
            await new PricingUnitOfWork(pricing, new OrderingTestDispatcher()).ExecuteInTransactionAsync(async ct =>
            {
                var lists = new PriceListRepository(pricing);
                var prices = new SkuPriceRepository(pricing);
                await lists.AddAsync(wholesaleList, ct);
                await lists.AddAsync(contractList, ct);
                await prices.AddAsync(wholesalePrice, ct);
                await prices.AddAsync(contractPrice, ct);
                await new ContractRepository(pricing).AddAsync(contract, ct);
                await new NegotiatedPriceRepository(pricing).AddAsync(negotiated, ct);
            });
            provider.GetRequiredService<IConfiguration>()["Pricing:DefaultPriceLists:Wholesale:VND"] = wholesaleList.Id.ToString();
            await provider.GetRequiredService<IInventoryModule>().ReceiveAsync(new(Guid.NewGuid(), item.Id, 30, "Test", Guid.NewGuid(), null), null);
        }

        var client = host.Client;
        var buyer = new BuyerSnapshot("Business", buyerId, customerId, Guid.NewGuid(), "Business buyer", "buyer@example.com", "0900000000");
        var wholesale = new AssistedOrderRequest("VND", OrderType.Wholesale, buyer, CheckoutTestScope.Address, null, new PaymentTerm("Net", 30), [new(item.Id, 2)]);
        const string route = "api/ordering/admin/orders/assisted";
        client.DefaultRequestHeaders.Add("Idempotency-Key", "assisted-wholesale");
        using (var anonymous = await client.PostAsJsonAsync(route, wholesale))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffId.ToString());
        using (var unprivileged = await client.PostAsJsonAsync(route, wholesale))
        {
            Assert.Equal(HttpStatusCode.Forbidden, unprivileged.StatusCode);
        }
        client.DefaultRequestHeaders.Add("X-Test-Admin", "true");

        async Task<(CheckoutResult Checkout, OrderReadService.OrderDetails Order)> Place(string key, AssistedOrderRequest request, decimal total)
        {
            client.DefaultRequestHeaders.Remove("Idempotency-Key");
            client.DefaultRequestHeaders.Add("Idempotency-Key", key);
            var placed = await OwnershipTests.Read<CheckoutResult>(await client.PostAsJsonAsync(route, request), 201);
            Assert.Equal(total, placed.GrandTotal);
            var replay = await OwnershipTests.Read<CheckoutResult>(await client.PostAsJsonAsync(route, request), 200);
            Assert.Equal(placed.OrderId, replay.OrderId);
            var detail = await OwnershipTests.Read<OrderReadService.OrderDetails>(await client.GetAsync($"api/ordering/admin/orders/{placed.OrderId}"), 200);
            Assert.Equal("Confirmed", detail.Status);
            Assert.Equal("Assisted", detail.OrderSource);
            Assert.Equal(buyer, detail.Buyer);
            Assert.Equal(new PaymentTerm("Net", 30), detail.PaymentTerm);
            Assert.Equal(total, detail.GrandTotal);
            return (placed, detail);
        }

        var wholesaleOrder = await Place("assisted-wholesale", wholesale, 160);
        Assert.Equal("Wholesale", wholesaleOrder.Order.OrderType);
        Assert.Null(wholesaleOrder.Order.ContractRef);
        var wholesaleSnapshot = Assert.Single(wholesaleOrder.Order.Lines).Price;
        Assert.Equal(("PriceList", wholesaleList.Id, wholesalePrice.Id), (wholesaleSnapshot.PriceSource, wholesaleSnapshot.PriceListId, wholesaleSnapshot.SkuPriceId));

        var contractRequest = wholesale with { OrderType = OrderType.Contract, ContractId = contract.Id };
        var contractOrder = await Place("assisted-contract", contractRequest, 140);
        Assert.Equal("Contract", contractOrder.Order.OrderType);
        Assert.Equal(contract.Id, contractOrder.Order.ContractRef);
        var contractSnapshot = Assert.Single(contractOrder.Order.Lines).Price;
        Assert.Equal("Contract", contractSnapshot.PriceSource);
        Assert.Equal(contractList.Id, contractSnapshot.PriceListId);
        Assert.Equal(contractPrice.Id, contractSnapshot.SkuPriceId);
        Assert.Equal(contract.Id, contractSnapshot.ContractId);

        var negotiatedRequest = wholesale with { Lines = [new(item.Id, 2, negotiated.Id)], NegotiationTransactionRef = negotiationRef };
        var negotiatedOrder = await Place("assisted-negotiated", negotiatedRequest, 110);
        var negotiatedSnapshot = Assert.Single(negotiatedOrder.Order.Lines).Price;
        Assert.Equal("Negotiated", negotiatedSnapshot.PriceSource);
        Assert.Equal(negotiated.Id, negotiatedSnapshot.NegotiatedPriceId);
        using (var verify = host.App.Services.CreateScope())
        {
            var stock = Assert.Single(await verify.ServiceProvider.GetRequiredService<IInventoryModule>().GetAvailabilityAsync([item.Id]));
            Assert.Equal((24, 6), (stock.Available, stock.Reserved));
        }
        await OwnershipTests.Read<OrderOperationResult>(await client.PostAsJsonAsync($"api/ordering/admin/orders/{negotiatedOrder.Checkout.OrderId}/cancel",
            new { negotiatedOrder.Checkout.ConcurrencyStamp, reason = "Customer cancelled" }), 200);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", "reuse-negotiated-after-cancel");
        using (var reused = await client.PostAsJsonAsync(route, negotiatedRequest))
        {
            Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        }
        client.DefaultRequestHeaders.Remove("X-Test-Admin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerId.ToString());
        var owned = await OwnershipTests.Read<OrderReadService.OrderDetails>(await client.GetAsync($"api/ordering/orders/{contractOrder.Checkout.OrderId}"), 200);
        Assert.Equal(contractOrder.Checkout.OrderId, owned.Id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", staffId.ToString());
        using var notBuyer = await client.GetAsync($"api/ordering/orders/{contractOrder.Checkout.OrderId}");
        Assert.Equal(HttpStatusCode.NotFound, notBuyer.StatusCode);
    }

    private static async Task<OrderingHttpHost> StartRealModules(string connection, OrderingTestClock clock)
    {
        var host = await OrderingHttpHost.Start(connection, clock, null, builder =>
        {
            foreach (var name in new[] { "Catalog", "Pricing", "Inventory" })
            {
                var assembly = Assembly.Load($"UltraSol.Modules.{name}.Api");
                assembly.GetType($"UltraSol.Modules.{name}.Api.{name}Module")!.GetMethod($"Add{name}Module")!
                    .Invoke(null, [builder.Services, builder.Configuration]);
                builder.Services.AddControllers().AddApplicationPart(assembly);
            }
            builder.Configuration["Ordering:ServiceAccount:Email"] = "ordering@test.local";
            builder.Configuration["Ordering:ServiceAccount:Password"] = "test-only";
        }, app => app.MapPost("/api/auth/token", () => Results.Json(ApiResultBuilder.Success(new { accessToken = HttpClientTests.Jwt(clock.Now.AddHours(1)) }))));
        using var migrate = host.App.Services.CreateScope();
        await migrate.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await migrate.ServiceProvider.GetRequiredService<PricingDbContext>().Database.MigrateAsync();
        await migrate.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
        return host;
    }
}