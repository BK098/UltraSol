using UltraSol.Modules.Ordering.Domain.Carts;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Domain.ValueObjects;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Ordering.Tests;

public sealed class DomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Cart_merges_quantity_changes_stamp_and_rejects_edits_during_checkout()
    {
        var cart = ShoppingCart.Create("guest:hash", null, "hash", "vnd", Now);
        var sku = Guid.NewGuid();
        var stamp = cart.ConcurrencyStamp;
        cart.AddItem(sku, 2, Now);
        cart.AddItem(sku, 3, Now);
        Assert.Equal(5, Assert.Single(cart.Items).Quantity);
        Assert.NotEqual(stamp, cart.ConcurrencyStamp);
        cart.BeginCheckout(Now);
        Assert.Throws<DomainException>(() => cart.Clear(Now));
    }

    [Fact]
    public void Cart_rejects_nonpositive_and_overflow_quantities_without_changing_line()
    {
        var cart = ShoppingCart.Create("user:1", Guid.NewGuid(), null, "VND", Now);
        var sku = Guid.NewGuid();
        Assert.Throws<DomainException>(() => cart.AddItem(sku, 0, Now));
        cart.AddItem(sku, int.MaxValue, Now);
        Assert.Throws<OverflowException>(() => cart.AddItem(sku, 1, Now));
        Assert.Equal(int.MaxValue, cart.Items.Single().Quantity);
    }

    [Fact]
    public void Order_computes_totals_from_snapshot_and_terminal_lifecycle_is_immutable()
    {
        var line = new OrderLineSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Original name", "SKU-1", "Black", null,
            3, new PriceSnapshot(120m, 120m, 0m, "VND", "PriceList", Guid.NewGuid(), null, null, null, null, null));
        var order = Order.Place(Guid.NewGuid(), "ORD-0000000001", null, "guest:hash", null, "hash", OrderType.Retail, "Storefront",
            new BuyerSnapshot("Guest", null, null, null, "Buyer", "buyer@example.com", "0900000000"),
            new AddressSnapshot("Buyer", "0900000000", "Street", null, null, null, null, null, "VN"), null,
            new PaymentTerm("COD", null), Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(15), "VND", [line], Now);
        Assert.Equal(360m, order.GrandTotal);
        Assert.Equal("Original name", order.Lines.Single().Snapshot.ProductName);
        order.Confirm(Now);
        order.Complete(Now.AddMinutes(5));
        Assert.Throws<DomainException>(() => order.Cancel("changed mind", Now.AddMinutes(6)));
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(3, order.History.Count);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("Net", 0)]
    [InlineData("COD", 30)]
    public void Invalid_payment_terms_are_rejected(string type, int? days)
    {
        Assert.Throws<DomainException>(() => new PaymentTerm(type, days).Validate());
    }
}