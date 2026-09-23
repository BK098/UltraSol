using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltraSol.Modules.Pricing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pricing");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "price_lists",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_lists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    commercial_terms = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    terminated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    terminated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contracts", x => x.id);
                    table.ForeignKey(
                        name: "FK_contracts_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalSchema: "pricing",
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "negotiated_prices",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    proposed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_negotiated_prices", x => x.id);
                    table.ForeignKey(
                        name: "FK_negotiated_prices_contracts_contract_id",
                        column: x => x.contract_id,
                        principalSchema: "pricing",
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sku_prices",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    last_mutation_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sku_prices", x => x.id);
                    table.ForeignKey(
                        name: "FK_sku_prices_contracts_contract_id",
                        column: x => x.contract_id,
                        principalSchema: "pricing",
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sku_prices_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalSchema: "pricing",
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_price_amendments",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_price_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    proposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    proposal_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_price_amendments", x => x.id);
                    table.ForeignKey(
                        name: "FK_contract_price_amendments_contracts_contract_id",
                        column: x => x.contract_id,
                        principalSchema: "pricing",
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contract_price_amendments_sku_prices_sku_price_id",
                        column: x => x.sku_price_id,
                        principalSchema: "pricing",
                        principalTable: "sku_prices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_changes",
                schema: "pricing",
                columns: table => new
                {
                    sku_price_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    before_json = table.Column<string>(type: "jsonb", nullable: false),
                    after_json = table.Column<string>(type: "jsonb", nullable: false),
                    contract_price_amendment_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_changes", x => new { x.sku_price_id, x.revision });
                    table.ForeignKey(
                        name: "FK_price_changes_sku_prices_sku_price_id",
                        column: x => x.sku_price_id,
                        principalSchema: "pricing",
                        principalTable: "sku_prices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_periods",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_price_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    contract_price_amendment_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_periods", x => x.id);
                    table.CheckConstraint("ck_price_periods_range", "effective_to IS NULL OR effective_to > effective_from");
                    table.ForeignKey(
                        name: "FK_price_periods_contract_price_amendments_contract_price_amen~",
                        column: x => x.contract_price_amendment_id,
                        principalSchema: "pricing",
                        principalTable: "contract_price_amendments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_price_periods_sku_prices_sku_price_id",
                        column: x => x.sku_price_id,
                        principalSchema: "pricing",
                        principalTable: "sku_prices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "price_tiers",
                schema: "pricing",
                columns: table => new
                {
                    price_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    minimum_quantity = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_tiers", x => new { x.price_period_id, x.minimum_quantity });
                    table.CheckConstraint("ck_price_tiers_values", "minimum_quantity > 0 AND amount >= 0");
                    table.ForeignKey(
                        name: "FK_price_tiers_price_periods_price_period_id",
                        column: x => x.price_period_id,
                        principalSchema: "pricing",
                        principalTable: "price_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contract_price_amendments_contract_id",
                schema: "pricing",
                table: "contract_price_amendments",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_price_amendments_sku_price_id_proposed_at",
                schema: "pricing",
                table: "contract_price_amendments",
                columns: new[] { "sku_price_id", "proposed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_contracts_customer_id_status",
                schema: "pricing",
                table: "contracts",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_contracts_price_list_id",
                schema: "pricing",
                table: "contracts",
                column: "price_list_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_negotiated_prices_contract_id",
                schema: "pricing",
                table: "negotiated_prices",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "IX_negotiated_prices_customer_id_status",
                schema: "pricing",
                table: "negotiated_prices",
                columns: new[] { "customer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_negotiated_prices_sku_id",
                schema: "pricing",
                table: "negotiated_prices",
                column: "sku_id");

            migrationBuilder.CreateIndex(
                name: "IX_negotiated_prices_transaction_id",
                schema: "pricing",
                table: "negotiated_prices",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_price_periods_contract_price_amendment_id",
                schema: "pricing",
                table: "price_periods",
                column: "contract_price_amendment_id");

            migrationBuilder.CreateIndex(
                name: "IX_price_periods_sku_price_id_effective_from",
                schema: "pricing",
                table: "price_periods",
                columns: new[] { "sku_price_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "IX_sku_prices_contract_id",
                schema: "pricing",
                table: "sku_prices",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "IX_sku_prices_price_list_id_sku_id",
                schema: "pricing",
                table: "sku_prices",
                columns: new[] { "price_list_id", "sku_id" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE pricing.price_periods ADD CONSTRAINT ex_price_periods_no_overlap
                EXCLUDE USING gist (sku_price_id WITH =, tstzrange(effective_from, effective_to, '[)') WITH &&)
                DEFERRABLE INITIALLY DEFERRED;

                CREATE FUNCTION pricing.protect_price_audit() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    RAISE EXCEPTION 'Price audit is immutable' USING ERRCODE = '23514';
                END;
                $body$;
                CREATE TRIGGER protect_price_audit BEFORE UPDATE OR DELETE ON pricing.price_changes
                FOR EACH ROW EXECUTE FUNCTION pricing.protect_price_audit();

                CREATE FUNCTION pricing.protect_price_amendment() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Amendments cannot be deleted' USING ERRCODE = '23514';
                    END IF;
                    IF OLD.status <> 0 OR NEW.id IS DISTINCT FROM OLD.id
                        OR NEW.sku_price_id IS DISTINCT FROM OLD.sku_price_id
                        OR NEW.contract_id IS DISTINCT FROM OLD.contract_id
                        OR NEW.proposed_at IS DISTINCT FROM OLD.proposed_at
                        OR NEW.proposal_json IS DISTINCT FROM OLD.proposal_json THEN
                        RAISE EXCEPTION 'Amendment proposal and decision are immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $body$;
                CREATE TRIGGER protect_price_amendment BEFORE UPDATE OR DELETE ON pricing.contract_price_amendments
                FOR EACH ROW EXECUTE FUNCTION pricing.protect_price_amendment();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "negotiated_prices",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "price_changes",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "price_tiers",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "price_periods",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "contract_price_amendments",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "sku_prices",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "contracts",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "price_lists",
                schema: "pricing");
            migrationBuilder.Sql("DROP FUNCTION pricing.protect_price_audit(); DROP FUNCTION pricing.protect_price_amendment();");
        }
    }
}