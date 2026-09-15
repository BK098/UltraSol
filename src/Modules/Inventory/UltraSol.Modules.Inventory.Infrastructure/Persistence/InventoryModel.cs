using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltraSol.Modules.Inventory.Domain;
using UltraSol.Modules.Inventory.Domain.Adjustments;
using UltraSol.Modules.Inventory.Domain.Reservations;
using UltraSol.Modules.Inventory.Domain.Stocks;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Infrastructure.Persistence.Extensions;

namespace UltraSol.Modules.Inventory.Infrastructure.Persistence;

internal static class InventoryModel
{
    internal static void Configure(ModelBuilder model)
    {
        var warehouse = model.SharedTypeEntity<Dictionary<string, object>>("InventoryWarehouse");
        warehouse.ToTable("warehouses");
        warehouse.IndexerProperty<Guid>("Id").ValueGeneratedNever();
        warehouse.HasKey("Id");
        warehouse.HasData((object)new Dictionary<string, object> { ["Id"] = InventoryDefaults.WarehouseId });

        var stock = ModelConfigure.Root<InventoryStock>(model, "stock_balances");
        ConfigureRoot(stock);
        stock.Ignore(value => value.Available);
        stock.HasIndex(value => new { value.WarehouseId, value.ProductItemId }).IsUnique().HasDatabaseName("ux_stock_balances_key");
        stock.HasOne("InventoryWarehouse", null).WithMany().HasForeignKey(nameof(InventoryStock.WarehouseId)).OnDelete(DeleteBehavior.Restrict);
        stock.ToTable("stock_balances", table => table.HasCheckConstraint("ck_stock_balances_quantity",
            "on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand"));

        var reservation = ModelConfigure.Root<StockReservation>(model, "stock_reservations");
        ConfigureRoot(reservation);
        reservation.Property(value => value.Status).HasConversion<int>().IsRequired();
        reservation.HasIndex(value => value.OrderId).IsUnique().HasDatabaseName("ux_stock_reservations_order");
        reservation.HasMany(value => value.Lines).WithOne().HasForeignKey("ReservationId").OnDelete(DeleteBehavior.Restrict);
        reservation.Navigation(value => value.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        reservation.ToTable("stock_reservations", table => table.HasCheckConstraint("ck_stock_reservations_status", "status IN (0,1,2,3)"));

        var reservationLine = ModelConfigure.Entity<ReservationLine>(model, "stock_reservation_lines");
        reservationLine.Property<Guid>("ReservationId");
        reservationLine.HasIndex("ReservationId", nameof(ReservationLine.WarehouseId), nameof(ReservationLine.ProductItemId))
            .IsUnique().HasDatabaseName("ux_stock_reservation_lines_key");
        reservationLine.HasOne("InventoryWarehouse", null).WithMany().HasForeignKey(nameof(ReservationLine.WarehouseId)).OnDelete(DeleteBehavior.Restrict);
        reservationLine.ToTable("stock_reservation_lines", table => table.HasCheckConstraint("ck_stock_reservation_lines_quantity", "quantity > 0"));

        var adjustment = ModelConfigure.Root<InventoryAdjustment>(model, "inventory_adjustments");
        ConfigureRoot(adjustment);
        adjustment.Property(value => value.Note).HasMaxLength(2000);
        adjustment.Property(value => value.Status).HasConversion<int>().IsRequired();
        adjustment.Property(value => value.Reason).HasConversion<int>().IsRequired();
        adjustment.HasMany(value => value.Lines).WithOne().HasForeignKey("AdjustmentId").OnDelete(DeleteBehavior.Restrict);
        adjustment.Navigation(value => value.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        adjustment.ToTable("inventory_adjustments", table =>
        {
            table.HasCheckConstraint("ck_inventory_adjustments_status", "status IN (0,1,2)");
            table.HasCheckConstraint("ck_inventory_adjustments_reason", "reason IN (0,1,2,3,4,5)");
        });

        var adjustmentLine = ModelConfigure.Entity<AdjustmentLine>(model, "inventory_adjustment_lines");
        adjustmentLine.Property<Guid>("AdjustmentId");
        adjustmentLine.HasIndex("AdjustmentId", nameof(AdjustmentLine.WarehouseId), nameof(AdjustmentLine.ProductItemId))
            .IsUnique().HasDatabaseName("ux_inventory_adjustment_lines_key");
        adjustmentLine.HasOne("InventoryWarehouse", null).WithMany().HasForeignKey(nameof(AdjustmentLine.WarehouseId)).OnDelete(DeleteBehavior.Restrict);
        adjustmentLine.ToTable("inventory_adjustment_lines", table => table.HasCheckConstraint("ck_inventory_adjustment_lines_quantity", "quantity_delta <> 0"));

        var movement = ModelConfigure.Entity<StockMovement>(model, "stock_movements");
        movement.Property(value => value.Type).HasConversion<int>().IsRequired();
        movement.Property(value => value.ReferenceType).HasMaxLength(100).IsRequired();
        movement.Property(value => value.Note).HasMaxLength(2000);
        movement.Property(value => value.ActorId).HasMaxLength(256);
        movement.HasOne("InventoryWarehouse", null).WithMany().HasForeignKey(nameof(StockMovement.WarehouseId)).OnDelete(DeleteBehavior.Restrict);
        movement.HasIndex(value => new { value.WarehouseId, value.ProductItemId, value.OccurredAt });
        movement.HasIndex(value => new { value.ReferenceType, value.ReferenceId });
        movement.ToTable("stock_movements", table =>
        {
            table.HasCheckConstraint("ck_stock_movements_type", "type IN (0,1,2)");
            table.HasCheckConstraint("ck_stock_movements_quantity", "quantity_delta <> 0");
            table.HasCheckConstraint("ck_stock_movements_direction",
                "(type = 0 AND quantity_delta > 0) OR (type = 1 AND quantity_delta < 0) OR type = 2");
        });
    }

    private static void ConfigureRoot<TEntity>(EntityTypeBuilder<TEntity> root)
        where TEntity : AggregateRoot
    {
        root.Property(value => value.ConcurrencyStamp).HasMaxLength(32);
        root.Property(value => value.CreatedBy).HasMaxLength(256);
        root.Property(value => value.UpdatedBy).HasMaxLength(256);
    }
}
