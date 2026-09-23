using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Domain.Carts;
using UltraSol.Modules.Ordering.Domain.Orders;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Infrastructure.Persistence.Extensions;

namespace UltraSol.Modules.Ordering.Infrastructure.Persistence;

internal static class OrderingModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema.Name);
        model.HasSequence<long>("order_number_seq", Schema.Name).StartsAt(1).IncrementsBy(1);
        ConfigureCart(model);
        ConfigureOrder(model);
        ConfigureProcesses(model);
    }

    private static void ConfigureCart(ModelBuilder model)
    {
        var cart = Root<ShoppingCart>(model, "carts");
        cart.Property(value => value.OwnerKey).HasMaxLength(100);
        cart.Property(value => value.GuestTokenHash).HasMaxLength(64);
        cart.Property(value => value.Currency).HasMaxLength(3);
        cart.Property(value => value.Status).HasConversion<int>();
        cart.HasMany(value => value.Items).WithOne().HasForeignKey("CartId").OnDelete(DeleteBehavior.Cascade);
        cart.Navigation(value => value.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);

        var item = ModelConfigure.Entity<CartItem>(model, "cart_items");
        item.Property<Guid>("CartId");
        item.HasIndex("CartId", nameof(CartItem.ProductItemId)).IsUnique().HasDatabaseName("ux_cart_items_product");
        item.ToTable("cart_items", table => table.HasCheckConstraint("ck_cart_items_quantity", "quantity > 0"));
    }

    private static void ConfigureOrder(ModelBuilder model)
    {
        var order = Root<Order>(model, "orders");
        order.Property(value => value.OrderNumber).HasMaxLength(64);
        order.Property(value => value.OwnerKey).HasMaxLength(100);
        order.Property(value => value.GuestTokenHash).HasMaxLength(64);
        order.Property(value => value.OrderType).HasConversion<int>();
        order.Property(value => value.OrderSource).HasMaxLength(100);
        order.Property(value => value.Currency).HasMaxLength(3);
        order.Property(value => value.Status).HasConversion<int>();
        Money(order.Property(value => value.Subtotal));
        Money(order.Property(value => value.DiscountTotal));
        Money(order.Property(value => value.TaxTotal));
        Money(order.Property(value => value.ShippingAmount));
        Money(order.Property(value => value.GrandTotal));
        OrderingJson.Configure(order.Property(value => value.Buyer));
        OrderingJson.Configure(order.Property(value => value.ShippingAddress));
        OrderingJson.Configure(order.Property(value => value.BillingAddress));
        OrderingJson.Configure(order.Property(value => value.PaymentTerm));
        OrderingJson.Configure(order.Property(value => value.ReservedStock));
        order.HasIndex(value => value.OrderNumber).IsUnique().HasDatabaseName("ux_orders_number");
        order.HasIndex(value => value.CartId).IsUnique().HasFilter("cart_id IS NOT NULL").HasDatabaseName("ux_orders_cart");
        order.HasMany(value => value.Lines).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Restrict);
        order.HasMany(value => value.History).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Restrict);
        order.HasMany(value => value.Notes).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Restrict);
        order.Navigation(value => value.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        order.Navigation(value => value.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
        order.Navigation(value => value.Notes).HasField("_notes").UsePropertyAccessMode(PropertyAccessMode.Field);

        var line = ModelConfigure.Entity<OrderLine>(model, "order_lines");
        line.Property<Guid>("OrderId");
        line.Property(value => value.ProductName).HasMaxLength(500);
        line.Property(value => value.SkuCode).HasMaxLength(250);
        line.Property(value => value.VariantDescription).HasMaxLength(500);
        line.Property(value => value.ImageUrl).HasMaxLength(2000);
        Money(line.Property(value => value.FinalUnitPrice));
        Money(line.Property(value => value.LineTotal));
        OrderingJson.Configure(line.Property(value => value.Price));
        line.HasIndex("OrderId", nameof(OrderLine.ProductItemId)).IsUnique().HasDatabaseName("ux_order_lines_product");
        line.ToTable("order_lines", table => table.HasCheckConstraint("ck_order_lines_quantity", "quantity > 0"));

        var history = ModelConfigure.Entity<OrderHistory>(model, "order_status_history");
        history.Property<Guid>("OrderId");
        history.Property(value => value.Status).HasConversion<int>();
        history.Property(value => value.Reason).HasMaxLength(2000);
        history.HasIndex("OrderId", nameof(OrderHistory.Version)).IsUnique().HasDatabaseName("ux_order_status_history_version");

        var note = ModelConfigure.Entity<OrderNote>(model, "order_notes");
        note.Property<Guid>("OrderId");
        note.Property(value => value.Text).HasMaxLength(4000);
    }

    private static void ConfigureProcesses(ModelBuilder model)
    {
        var attempt = model.Entity<CheckoutAttempt>();
        attempt.ToTable("checkout_attempts");
        attempt.HasKey(value => value.Id);
        attempt.Property(value => value.Id).ValueGeneratedNever();
        attempt.Property(value => value.OwnerKey).HasMaxLength(100);
        attempt.Property(value => value.GuestTokenHash).HasMaxLength(64);
        attempt.Property(value => value.IdempotencyKey).HasMaxLength(200);
        attempt.Property(value => value.Fingerprint).HasMaxLength(64);
        attempt.Property(value => value.Stage).HasConversion<int>();
        attempt.Property(value => value.ErrorCode).HasMaxLength(100);
        attempt.Property(value => value.ErrorMessage).HasMaxLength(4000);
        attempt.Property(value => value.Version).HasMaxLength(32).IsConcurrencyToken();
        OrderingJson.Configure(attempt.Property(value => value.Input));
        OrderingJson.Configure(attempt.Property(value => value.CatalogItems));
        OrderingJson.Configure(attempt.Property(value => value.Quote));
        OrderingJson.Configure(attempt.Property(value => value.StockLines));
        OrderingJson.Configure(attempt.Property(value => value.Result));
        attempt.HasIndex(value => new { value.OwnerKey, value.IdempotencyKey }).IsUnique().HasDatabaseName("ux_checkout_attempts_owner_key");
        attempt.HasIndex(value => value.OrderId).IsUnique().HasDatabaseName("ux_checkout_attempts_order");
        attempt.HasIndex(value => value.CartId).IsUnique().HasFilter("cart_id IS NOT NULL AND stage NOT IN (3, 5)")
            .HasDatabaseName("ux_checkout_attempts_active_cart");
        attempt.HasIndex(value => value.NegotiationTransactionRef).IsUnique()
            .HasFilter("negotiation_transaction_ref IS NOT NULL AND stage <> 5").HasDatabaseName("ux_checkout_attempts_negotiation");
        attempt.HasIndex(value => new { value.Stage, value.LeaseUntil, value.CreatedAt }).HasDatabaseName("ix_checkout_attempts_pending");

        var operation = model.Entity<OrderOperation>();
        operation.ToTable("order_operations");
        operation.HasKey(value => value.Id);
        operation.Property(value => value.Id).ValueGeneratedNever();
        operation.Property(value => value.Kind).HasMaxLength(100);
        operation.Property(value => value.Reason).HasMaxLength(2000);
        operation.Property(value => value.State).HasMaxLength(50);
        operation.Property(value => value.ErrorCode).HasMaxLength(100);
        operation.Property(value => value.Version).HasMaxLength(32).IsConcurrencyToken();
        operation.HasIndex(value => new { value.OrderId, value.Kind }).IsUnique().HasDatabaseName("ux_order_operations_order_kind");
        operation.HasIndex(value => new { value.State, value.LeaseUntil, value.CreatedAt }).HasDatabaseName("ix_order_operations_pending");
    }

    private static EntityTypeBuilder<T> Root<T>(ModelBuilder model, string table)
        where T : AggregateRoot => ModelConfigure.Root<T>(model, table);

    private static void Money(PropertyBuilder<decimal> property) => property.HasColumnType("numeric");
}