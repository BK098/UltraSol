using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Domain.Carts;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Messaging;
using Xunit;

namespace UltraSol.Modules.Ordering.Tests;

public sealed class ModelTests
{
    private static OrderingDbContext CreateContext() => new(new DbContextOptionsBuilder<OrderingDbContext>()
        .UseNpgsql("Host=localhost;Database=ordering_model_only;Username=unused")
        .Options);

    [Fact]
    public void ModelUsesOneOrderingSchemaAndTheRequiredTables()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var tables = model.GetEntityTypes().Select(entity => entity.GetTableName()!).ToHashSet();

        Assert.All(model.GetEntityTypes(), entity => Assert.Equal("ordering", entity.GetSchema()));
        Assert.Subset(tables, new HashSet<string>
        {
            "carts", "cart_items", "orders", "order_lines", "order_status_history", "order_notes",
            "checkout_attempts", "order_operations", "outbox", "inbox"
        });
        Assert.Equal("order_number_seq", Assert.Single(model.GetSequences()).Name);
        Assert.Equal("ordering", Assert.Single(model.GetSequences()).Schema);
        Assert.Equal(1L, Assert.Single(model.GetSequences()).StartValue);
    }

    [Fact]
    public void AggregateChildrenUseBackingFieldsAndProtectOrderRows()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var cart = model.FindEntityType(typeof(ShoppingCart))!;
        var order = model.FindEntityType(typeof(Order))!;

        Assert.True(cart.FindProperty(nameof(ShoppingCart.ConcurrencyStamp))!.IsConcurrencyToken);
        Assert.True(order.FindProperty(nameof(Order.ConcurrencyStamp))!.IsConcurrencyToken);
        Assert.Equal("_items", cart.FindNavigation(nameof(ShoppingCart.Items))!.FieldInfo!.Name);
        Assert.Equal("_lines", order.FindNavigation(nameof(Order.Lines))!.FieldInfo!.Name);
        Assert.Equal("_history", order.FindNavigation(nameof(Order.History))!.FieldInfo!.Name);
        Assert.Equal("_notes", order.FindNavigation(nameof(Order.Notes))!.FieldInfo!.Name);
        Assert.Equal(DeleteBehavior.Cascade, cart.FindNavigation(nameof(ShoppingCart.Items))!.ForeignKey.DeleteBehavior);
        Assert.All(order.GetNavigations().Select(navigation => navigation.ForeignKey), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void SnapshotsAndProcessPayloadsUseJsonbWhileMoneyAndQuantityRemainColumns()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var order = model.FindEntityType(typeof(Order))!;
        var line = model.FindEntityType(typeof(OrderLine))!;
        var attempt = model.FindEntityType(typeof(CheckoutAttempt))!;

        Assert.Equal("jsonb", order.FindProperty(nameof(Order.Buyer))!.GetColumnType());
        Assert.Equal("jsonb", order.FindProperty(nameof(Order.ShippingAddress))!.GetColumnType());
        Assert.Equal("jsonb", order.FindProperty(nameof(Order.BillingAddress))!.GetColumnType());
        Assert.Equal("jsonb", order.FindProperty(nameof(Order.PaymentTerm))!.GetColumnType());
        Assert.Equal("jsonb", order.FindProperty(nameof(Order.ReservedStock))!.GetColumnType());
        Assert.Equal("numeric", order.FindProperty(nameof(Order.GrandTotal))!.GetColumnType());
        Assert.Equal("numeric", line.FindProperty(nameof(OrderLine.FinalUnitPrice))!.GetColumnType());
        Assert.Equal("jsonb", line.FindProperty(nameof(OrderLine.Price))!.GetColumnType());
        Assert.Equal("integer", line.FindProperty(nameof(OrderLine.Quantity))!.GetColumnType());
        Assert.Equal("jsonb", attempt.FindProperty(nameof(CheckoutAttempt.Input))!.GetColumnType());
        Assert.Equal("jsonb", attempt.FindProperty(nameof(CheckoutAttempt.CatalogItems))!.GetColumnType());
        Assert.Equal("jsonb", attempt.FindProperty(nameof(CheckoutAttempt.Quote))!.GetColumnType());
        Assert.Equal("jsonb", attempt.FindProperty(nameof(CheckoutAttempt.StockLines))!.GetColumnType());
        Assert.Equal("jsonb", attempt.FindProperty(nameof(CheckoutAttempt.Result))!.GetColumnType());
    }

    [Fact]
    public void DatabaseConstraintsProtectOrderingIdentitiesAndProcessClaims()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var order = model.FindEntityType(typeof(Order))!;
        var attempt = model.FindEntityType(typeof(CheckoutAttempt))!;
        var operation = model.FindEntityType(typeof(OrderOperation))!;

        Assert.Contains(order.GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "ux_orders_number");
        Assert.Contains(order.GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "ux_orders_cart" && index.GetFilter() == "cart_id IS NOT NULL");
        Assert.Contains(attempt.GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "ux_checkout_attempts_owner_key");
        Assert.Contains(attempt.GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "ux_checkout_attempts_order");
        Assert.Contains(attempt.GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "ux_checkout_attempts_active_cart" && index.GetFilter()!.Contains("stage NOT IN", StringComparison.Ordinal));
        Assert.Contains(attempt.GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "ux_checkout_attempts_negotiation" && index.GetFilter()!.Contains("stage <> 5", StringComparison.Ordinal));
        Assert.True(attempt.FindProperty(nameof(CheckoutAttempt.Version))!.IsConcurrencyToken);
        Assert.True(operation.FindProperty(nameof(OrderOperation.Version))!.IsConcurrencyToken);
        Assert.Equal("ordering", model.FindEntityType(typeof(OutboxMessage))!.GetSchema());
        Assert.Equal("ordering", model.FindEntityType(typeof(InboxMessage))!.GetSchema());
    }
}