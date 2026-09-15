using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltraSol.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "inbox",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_adjustments",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_adjustments", x => x.id);
                    table.CheckConstraint("ck_inventory_adjustments_reason", "reason IN (0,1,2,3,4,5)");
                    table.CheckConstraint("ck_inventory_adjustments_status", "status IN (0,1,2)");
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservations", x => x.id);
                    table.CheckConstraint("ck_stock_reservations_status", "status IN (0,1,2,3)");
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_adjustment_lines",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_delta = table.Column<int>(type: "integer", nullable: false),
                    adjustment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_adjustment_lines", x => x.id);
                    table.CheckConstraint("ck_inventory_adjustment_lines_quantity", "quantity_delta <> 0");
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_lines_inventory_adjustments_adjustment~",
                        column: x => x.adjustment_id,
                        principalSchema: "inventory",
                        principalTable: "inventory_adjustments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_lines_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_balances",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    on_hand = table.Column<int>(type: "integer", nullable: false),
                    reserved = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_balances", x => x.id);
                    table.CheckConstraint("ck_stock_balances_quantity", "on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand");
                    table.ForeignKey(
                        name: "FK_stock_balances_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    quantity_delta = table.Column<int>(type: "integer", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    actor_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.id);
                    table.CheckConstraint("ck_stock_movements_direction", "(type = 0 AND quantity_delta > 0) OR (type = 1 AND quantity_delta < 0) OR type = 2");
                    table.CheckConstraint("ck_stock_movements_quantity", "quantity_delta <> 0");
                    table.CheckConstraint("ck_stock_movements_type", "type IN (0,1,2)");
                    table.ForeignKey(
                        name: "FK_stock_movements_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservation_lines",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservation_lines", x => x.id);
                    table.CheckConstraint("ck_stock_reservation_lines_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_stock_reservation_lines_stock_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "inventory",
                        principalTable: "stock_reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_reservation_lines_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "inventory",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "warehouses",
                column: "id",
                value: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustment_lines_warehouse_id",
                schema: "inventory",
                table: "inventory_adjustment_lines",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ux_inventory_adjustment_lines_key",
                schema: "inventory",
                table: "inventory_adjustment_lines",
                columns: new[] { "adjustment_id", "warehouse_id", "product_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_published_at_created_at",
                schema: "inventory",
                table: "outbox",
                columns: new[] { "published_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_stock_balances_key",
                schema: "inventory",
                table: "stock_balances",
                columns: new[] { "warehouse_id", "product_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_reference_type_reference_id",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "reference_type", "reference_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_warehouse_id_product_item_id_occurred_at",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "product_item_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_reservation_lines_warehouse_id",
                schema: "inventory",
                table: "stock_reservation_lines",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ux_stock_reservation_lines_key",
                schema: "inventory",
                table: "stock_reservation_lines",
                columns: new[] { "reservation_id", "warehouse_id", "product_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_stock_reservations_order",
                schema: "inventory",
                table: "stock_reservations",
                column: "order_id",
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION inventory.reject_movement_mutation() RETURNS trigger
                LANGUAGE plpgsql AS $body$
                BEGIN
                    RAISE EXCEPTION 'Stock movements are append-only';
                END;
                $body$;
                CREATE TRIGGER stock_movements_append_only
                BEFORE UPDATE OR DELETE ON inventory.stock_movements
                FOR EACH ROW EXECUTE FUNCTION inventory.reject_movement_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER stock_movements_append_only ON inventory.stock_movements; DROP FUNCTION inventory.reject_movement_mutation();");
            migrationBuilder.DropTable(
                name: "inbox",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inventory_adjustment_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_balances",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_reservation_lines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inventory_adjustments",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_reservations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "inventory");
        }
    }
}
