using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using UltraSol.Modules.Inventory.Domain;
using UltraSol.Modules.Inventory.Domain.Adjustments;
using UltraSol.Modules.Inventory.Domain.Reservations;
using UltraSol.Modules.Inventory.Domain.Stocks;
using UltraSol.Modules.Inventory.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Messaging;
using Xunit;

namespace UltraSol.Modules.Inventory.Tests;

public sealed class ModelTests
{
    private static InventoryDbContext CreateContext() => new(new DbContextOptionsBuilder<InventoryDbContext>()
        .UseNpgsql("Host=localhost;Database=inventory_model_only;Username=unused")
        .Options);

    [Fact]
    public void WarehouseIsAnInventoryOwnedIdOnlySeed()
    {
        using var context = CreateContext();
        var warehouse = context.GetService<IDesignTimeModel>().Model.FindEntityType("InventoryWarehouse")!;

        Assert.Equal(typeof(Dictionary<string, object>), warehouse.ClrType);
        Assert.Equal("inventory", warehouse.GetSchema());
        Assert.Equal("warehouses", warehouse.GetTableName());
        Assert.Equal(["Id"], warehouse.GetProperties().Select(property => property.Name));
        Assert.Equal(["Id"], warehouse.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(InventoryDefaults.WarehouseId, warehouse.GetSeedData().Single()["Id"]);
        Assert.DoesNotContain(context.GetService<IDesignTimeModel>().Model.GetEntityTypes(), entity => entity.ClrType.Name == "Warehouse");
    }

    [Fact]
    public void StockUsesOneConcurrencyTokenAndDatabaseBalanceRules()
    {
        using var context = CreateContext();
        var stock = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(InventoryStock))!;

        Assert.Equal("inventory", stock.GetSchema());
        Assert.Equal("stock_balances", stock.GetTableName());
        Assert.True(stock.FindProperty(nameof(InventoryStock.ConcurrencyStamp))!.IsConcurrencyToken);
        Assert.DoesNotContain(stock.GetProperties(), property => property.Name is "Available" or "Version" or "xmin");
        Assert.Contains(stock.GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "ux_stock_balances_key" &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(InventoryStock.WarehouseId), nameof(InventoryStock.ProductItemId)]));
        Assert.Contains(stock.GetCheckConstraints(), constraint => constraint.Sql.Contains("reserved <= on_hand", StringComparison.Ordinal));
    }

    [Fact]
    public void AggregateLinesUseBackingFieldsAndRestrictForeignKeys()
    {
        using var context = CreateContext();
        var reservation = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(StockReservation))!;
        var reservationLine = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ReservationLine))!;
        var adjustment = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(InventoryAdjustment))!;
        var adjustmentLine = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(AdjustmentLine))!;

        Assert.Equal("_lines", reservation.FindNavigation(nameof(StockReservation.Lines))!.FieldInfo!.Name);
        Assert.Equal("_lines", adjustment.FindNavigation(nameof(InventoryAdjustment.Lines))!.FieldInfo!.Name);
        Assert.NotNull(reservationLine.FindProperty("ReservationId"));
        Assert.NotNull(adjustmentLine.FindProperty("AdjustmentId"));
        Assert.Contains(reservation.GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "ux_stock_reservations_order");
        Assert.Contains(reservationLine.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(["ReservationId", nameof(ReservationLine.WarehouseId), nameof(ReservationLine.ProductItemId)]));
        Assert.Contains(adjustmentLine.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual(["AdjustmentId", nameof(AdjustmentLine.WarehouseId), nameof(AdjustmentLine.ProductItemId)]));
        Assert.All(context.GetService<IDesignTimeModel>().Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void MovementAndMailboxUseInventorySchemaAndSnakeCaseColumns()
    {
        using var context = CreateContext();
        var movement = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(StockMovement))!;
        var tables = context.GetService<IDesignTimeModel>().Model.GetEntityTypes();

        Assert.Equal("stock_movements", movement.GetTableName());
        Assert.Contains(movement.GetCheckConstraints(), constraint => constraint.Sql.Contains("quantity_delta <> 0", StringComparison.Ordinal));
        Assert.Equal("reference_type", movement.FindProperty(nameof(StockMovement.ReferenceType))!.GetColumnName());
        Assert.Equal("occurred_at", movement.FindProperty(nameof(StockMovement.OccurredAt))!.GetColumnName());
        Assert.Equal("inventory", tables.Single(entity => entity.ClrType == typeof(OutboxMessage)).GetSchema());
        Assert.Equal("published_at", tables.Single(entity => entity.ClrType == typeof(OutboxMessage))
            .FindProperty(nameof(OutboxMessage.PublishedAt))!.GetColumnName());
        Assert.Equal("received_at", tables.Single(entity => entity.ClrType == typeof(InboxMessage))
            .FindProperty(nameof(InboxMessage.ReceivedAt))!.GetColumnName());
        Assert.DoesNotContain(tables, entity => entity.ClrType.Namespace?.Contains("Catalog", StringComparison.Ordinal) == true);
    }
}