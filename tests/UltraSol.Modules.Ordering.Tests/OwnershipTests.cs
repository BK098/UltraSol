using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Shared.Application.Responses;
using Xunit;

namespace UltraSol.Modules.Ordering.Tests;

public sealed class OwnershipTests(OrderingDatabaseFixture fixture) : IClassFixture<OrderingDatabaseFixture>
{
    [OrderingPostgresFact]
    public async Task Guest_HTTP_requires_secret_and_admin_permissions_while_checkout_uses_only_server_identity()
    {
        using var db = fixture.Create();
        var clock = new OrderingTestClock();
        var dependencies = new CheckoutDependencies(clock);
        await using var host = await OrderingHttpHost.Start(db.Database.GetConnectionString()!, clock, dependencies);
        var client = host.Client;
        var created = await Read<CartService.CartResponse>(await client.PostAsJsonAsync("api/ordering/carts", new { currency = "VND" }), 201);
        Assert.Equal(64, created.GuestAccessToken!.Length);
        using var missing = await client.GetAsync($"api/ordering/carts/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        client.DefaultRequestHeaders.Add("X-Ordering-Guest-Token", OrderingAccess.NewToken());
        using var wrong = await client.GetAsync($"api/ordering/carts/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Ordering-Guest-Token");
        client.DefaultRequestHeaders.Add("X-Ordering-Guest-Token", created.GuestAccessToken);
        var cart = await Read<CartService.CartResponse>(await client.PostAsJsonAsync($"api/ordering/carts/{created.Id}/items",
            new { created.ConcurrencyStamp, productItemId = Guid.NewGuid(), quantity = 2 }), 200);
        Assert.Null(cart.GuestAccessToken);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "guest-http");
        var fakeAccount = Guid.NewGuid();
        var body = new
        {
            cart.ConcurrencyStamp, buyer = new { name = "Buyer", email = "buyer@example.com", phone = "0900000000", identityUserId = fakeAccount, customerId = fakeAccount },
            shippingAddress = CheckoutTestScope.Address, paymentTerm = new { type = "COD" }, grandTotal = 1, priceListId = fakeAccount
        };
        var checkout = await Read<CheckoutResult>(await client.PostAsJsonAsync($"api/ordering/carts/{cart.Id}/checkout", body), 201);
        var replay = await Read<CheckoutResult>(await client.PostAsJsonAsync($"api/ordering/carts/{cart.Id}/checkout", body), 200);
        Assert.Equal(checkout.OrderId, replay.OrderId);
        Assert.Equal(240, checkout.GrandTotal);
        using var orderResponse = await client.GetAsync($"api/ordering/orders/{checkout.OrderId}");
        var order = (await orderResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(JsonValueKind.Null, order.GetProperty("buyer").GetProperty("identityUserId").ValueKind);
        Assert.Equal(JsonValueKind.Null, order.GetProperty("buyer").GetProperty("customerId").ValueKind);
        client.DefaultRequestHeaders.Remove("X-Ordering-Guest-Token");
        using var orderMissing = await client.GetAsync($"api/ordering/orders/{checkout.OrderId}");
        Assert.Equal(HttpStatusCode.NotFound, orderMissing.StatusCode);
        using var anonymousAdmin = await client.GetAsync("api/ordering/admin/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousAdmin.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Guid.NewGuid().ToString());
        using var unprivileged = await client.GetAsync("api/ordering/admin/orders");
        Assert.Equal(HttpStatusCode.Forbidden, unprivileged.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Admin", "true");
        using var admin = await client.GetAsync("api/ordering/admin/orders");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
    }

    [OrderingPostgresFact]
    public async Task Registered_cart_and_mine_are_scoped_to_current_account()
    {
        using var db = fixture.Create();
        var clock = new OrderingTestClock();
        await using var host = await OrderingHttpHost.Start(db.Database.GetConnectionString()!, clock, new(clock));
        var client = host.Client;
        var actor = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", actor.ToString());
        var created = await Read<CartService.CartResponse>(await client.PostAsJsonAsync("api/ordering/carts", new { currency = "VND" }), 201);
        Assert.Null(created.GuestAccessToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Guid.NewGuid().ToString());
        using var other = await client.GetAsync($"api/ordering/carts/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        using var mine = await client.GetAsync("api/ordering/orders/mine");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await client.GetAsync("api/ordering/orders/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    internal static async Task<T> Read<T>(HttpResponseMessage response, int status)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {text}");
            return JsonSerializer.Deserialize<ApiResult<T>>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Data!;
        }
    }
}