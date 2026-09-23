using UltraSol.Modules.Ordering.Application.Checkout;
using Xunit;

namespace UltraSol.Modules.Ordering.Tests;

public sealed class CheckoutRulesTests
{
    [Fact]
    public void Malformed_dependency_lines_are_retryable_dependency_failures()
    {
        var item = Guid.NewGuid();
        var input = new QuoteRequest("Retail", "VND", null, null, null, [new(item, 1)]);
        var line = new QuoteLine(item, 1, 10, 10, 0, 10, "PriceList", Guid.NewGuid(), null, null, null, null, null);
        var quote = new PriceQuote(Guid.NewGuid(), DateTimeOffset.UtcNow, "VND", [line], 10, 0, 0, 0, 10);
        foreach (var invalid in new[] { quote with { Lines = [null!] }, quote with { Lines = [line with { PriceSource = "Unknown" }] },
            quote with { Lines = [line with { ListUnitPrice = -1 }] } })
        {
            var error = Assert.Throws<OrderingFailure>(() => CheckoutRules.ValidateQuote(input, invalid));
            Assert.Equal(503, error.Status);
            Assert.Equal("InvalidPricingResponse", error.Code);
        }
        var catalog = new CatalogItem(item, Guid.NewGuid(), "SKU", "Product", "", null, true, null, true, [null!]);
        foreach (var invalid in new[] { new CatalogItem[] { null! }, new[] { catalog } })
        {
            var error = Assert.Throws<OrderingFailure>(() => CheckoutRules.Expand(input.Lines, invalid));
            Assert.Equal(503, error.Status);
            Assert.Equal("InvalidCatalogResponse", error.Code);
        }
        var attempt = new CheckoutAttempt { OrderId = Guid.NewGuid(), ExpiresAt = DateTimeOffset.UtcNow, StockLines = [new(item, 1)] };
        var reservation = new Reservation(Guid.NewGuid(), attempt.OrderId, "Active", attempt.ExpiresAt.Value, [null!]);
        var inventoryError = Assert.Throws<OrderingFailure>(() => CheckoutRules.ValidateReservation(attempt, reservation));
        Assert.Equal(503, inventoryError.Status);
        Assert.Equal("InvalidInventoryResponse", inventoryError.Code);
    }

    [Fact]
    public void Bundle_expansion_groups_components_with_regular_items()
    {
        var bundle = Guid.NewGuid();
        var component = Guid.NewGuid();
        var lines = CheckoutRules.Expand([new RequestedLine(bundle, 3), new RequestedLine(component, 2)],
            [new CatalogItem(bundle, Guid.NewGuid(), "B", "Bundle", "", null, true, null, true, [new StockLine(component, 4)]),
             new CatalogItem(component, Guid.NewGuid(), "C", "Component", "", null, true, null, false, [])]);
        Assert.Equal(14, Assert.Single(lines).Quantity);
        Assert.Equal(component, lines[0].ProductItemId);
    }

    [Fact]
    public void Missing_unsellable_or_overflow_bundle_is_not_partially_reserved()
    {
        var id = Guid.NewGuid();
        Assert.Throws<OrderingFailure>(() => CheckoutRules.Expand([new RequestedLine(id, 1)], []));
        Assert.Throws<OverflowException>(() => CheckoutRules.Expand([new RequestedLine(id, int.MaxValue)],
            [new CatalogItem(id, Guid.NewGuid(), "B", "Bundle", "", null, true, null, true, [new StockLine(Guid.NewGuid(), 2)])]));
    }

    [Fact]
    public void Guest_access_requires_secret_and_does_not_accept_cart_id()
    {
        var token = OrderingAccess.NewToken();
        var hash = OrderingAccess.Hash(token);
        Assert.True(OrderingAccess.Matches(hash, token));
        Assert.False(OrderingAccess.Matches(hash, OrderingAccess.NewToken()));
        Assert.False(OrderingAccess.Matches(hash, Guid.NewGuid().ToString()));
    }
}