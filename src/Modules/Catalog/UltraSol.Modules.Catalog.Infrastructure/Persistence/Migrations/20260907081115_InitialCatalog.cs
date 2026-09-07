using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "brands",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.CheckConstraint("ck_category_parent", "parent_category_id IS NULL OR parent_category_id <> id");
                    table.ForeignKey(
                        name: "FK_categories_categories_parent_category_id",
                        column: x => x.parent_category_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collections",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    match_mode = table.Column<int>(type: "integer", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collections", x => x.id);
                    table.CheckConstraint("ck_collection_type", "(type = 1 AND match_mode IS NULL) OR (type = 2 AND match_mode IS NOT NULL AND match_mode IN (1,2))");
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    is_structure_locked = table.Column<bool>(type: "boolean", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.CheckConstraint("ck_product_status", "status IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_products_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "catalog",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collection_rule_brands",
                schema: "catalog",
                columns: table => new
                {
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_rule_brands", x => new { x.collection_id, x.brand_id });
                    table.ForeignKey(
                        name: "FK_collection_rule_brands_brands_brand_id",
                        column: x => x.brand_id,
                        principalSchema: "catalog",
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_rule_brands_collections_collection_id",
                        column: x => x.collection_id,
                        principalSchema: "catalog",
                        principalTable: "collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collection_rule_categories",
                schema: "catalog",
                columns: table => new
                {
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_rule_categories", x => new { x.collection_id, x.category_id });
                    table.ForeignKey(
                        name: "FK_collection_rule_categories_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_collection_rule_categories_collections_collection_id",
                        column: x => x.collection_id,
                        principalSchema: "catalog",
                        principalTable: "collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collection_entries",
                schema: "catalog",
                columns: table => new
                {
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_entries", x => new { x.collection_id, x.product_id });
                    table.CheckConstraint("ck_collection_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_collection_entries_collections_collection_id",
                        column: x => x.collection_id,
                        principalSchema: "catalog",
                        principalTable: "collections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_collection_entries_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                schema: "catalog",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => new { x.product_id, x.category_id });
                    table.ForeignKey(
                        name: "FK_product_categories_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_categories_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_items",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    option_signature = table.Column<string>(type: "text", nullable: false),
                    is_bundle = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_items", x => x.id);
                    table.UniqueConstraint("AK_product_items_id_product_id", x => new { x.id, x.product_id });
                    table.ForeignKey(
                        name: "FK_product_items_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_media",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    alt_text = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_media", x => x.id);
                    table.CheckConstraint("ck_product_media_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_product_media_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "variations",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_variations", x => x.id);
                    table.UniqueConstraint("AK_variations_id_product_id", x => new { x.id, x.product_id });
                    table.ForeignKey(
                        name: "FK_variations_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bundle_components",
                schema: "catalog",
                columns: table => new
                {
                    bundle_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bundle_components", x => new { x.bundle_item_id, x.component_item_id });
                    table.CheckConstraint("ck_bundle_component", "quantity > 0 AND bundle_item_id <> component_item_id");
                    table.ForeignKey(
                        name: "FK_bundle_components_product_items_bundle_item_id",
                        column: x => x.bundle_item_id,
                        principalSchema: "catalog",
                        principalTable: "product_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bundle_components_product_items_component_item_id",
                        column: x => x.component_item_id,
                        principalSchema: "catalog",
                        principalTable: "product_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_item_media",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    alt_text = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    product_item_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_item_media", x => x.id);
                    table.CheckConstraint("ck_product_item_media_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_product_item_media_product_items_product_item_id",
                        column: x => x.product_item_id,
                        principalSchema: "catalog",
                        principalTable: "product_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "variation_options",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    variation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_variation_options", x => x.id);
                    table.UniqueConstraint("AK_variation_options_id_variation_id", x => new { x.id, x.variation_id });
                    table.ForeignKey(
                        name: "FK_variation_options_variations_variation_id",
                        column: x => x.variation_id,
                        principalSchema: "catalog",
                        principalTable: "variations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_item_selections",
                schema: "catalog",
                columns: table => new
                {
                    product_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    option_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_item_selections", x => new { x.product_item_id, x.variation_id });
                    table.ForeignKey(
                        name: "FK_product_item_selections_product_items_product_item_id_produ~",
                        columns: x => new { x.product_item_id, x.product_id },
                        principalSchema: "catalog",
                        principalTable: "product_items",
                        principalColumns: new[] { "id", "product_id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_item_selections_variation_options_option_id_variati~",
                        columns: x => new { x.option_id, x.variation_id },
                        principalSchema: "catalog",
                        principalTable: "variation_options",
                        principalColumns: new[] { "id", "variation_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_item_selections_variations_variation_id_product_id",
                        columns: x => new { x.variation_id, x.product_id },
                        principalSchema: "catalog",
                        principalTable: "variations",
                        principalColumns: new[] { "id", "product_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bundle_components_component_item_id",
                schema: "catalog",
                table: "bundle_components",
                column: "component_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_categories_parent_category_id",
                schema: "catalog",
                table: "categories",
                column: "parent_category_id");

            migrationBuilder.CreateIndex(
                name: "IX_collection_entries_collection_id_sort_order",
                schema: "catalog",
                table: "collection_entries",
                columns: new[] { "collection_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_entries_product_id_collection_id",
                schema: "catalog",
                table: "collection_entries",
                columns: new[] { "product_id", "collection_id" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_rule_brands_brand_id",
                schema: "catalog",
                table: "collection_rule_brands",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_collection_rule_categories_category_id",
                schema: "catalog",
                table: "collection_rule_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_categories_category_id_product_id",
                schema: "catalog",
                table: "product_categories",
                columns: new[] { "category_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_item_media_product_item_id",
                schema: "catalog",
                table: "product_item_media",
                column: "product_item_id",
                unique: true,
                filter: "is_primary");

            migrationBuilder.CreateIndex(
                name: "IX_product_item_media_product_item_id_sort_order",
                schema: "catalog",
                table: "product_item_media",
                columns: new[] { "product_item_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_product_item_selections_option_id_variation_id",
                schema: "catalog",
                table: "product_item_selections",
                columns: new[] { "option_id", "variation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_item_selections_product_item_id_product_id",
                schema: "catalog",
                table: "product_item_selections",
                columns: new[] { "product_item_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_item_selections_variation_id_product_id",
                schema: "catalog",
                table: "product_item_selections",
                columns: new[] { "variation_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_items_product_id",
                schema: "catalog",
                table: "product_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_items_sku",
                schema: "catalog",
                table: "product_items",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_media_product_id",
                schema: "catalog",
                table: "product_media",
                column: "product_id",
                unique: true,
                filter: "is_primary");

            migrationBuilder.CreateIndex(
                name: "IX_product_media_product_id_sort_order",
                schema: "catalog",
                table: "product_media",
                columns: new[] { "product_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_products_brand_id",
                schema: "catalog",
                table: "products",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_status_id",
                schema: "catalog",
                table: "products",
                columns: new[] { "status", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_variation_options_variation_id",
                schema: "catalog",
                table: "variation_options",
                column: "variation_id");

            migrationBuilder.CreateIndex(
                name: "IX_variations_product_id",
                schema: "catalog",
                table: "variations",
                column: "product_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bundle_components",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "collection_entries",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "collection_rule_brands",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "collection_rule_categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_item_media",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_item_selections",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_media",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "collections",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_items",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "variation_options",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "variations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "products",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "brands",
                schema: "catalog");
        }
    }
}
