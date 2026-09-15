using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceCatalogIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION catalog.lock_integrity_write() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    -- Fresh snapshots after waiting for the existing Catalog transaction lock are required.
                    IF current_setting('transaction_isolation') NOT IN ('read committed', 'read uncommitted') THEN
                        RAISE EXCEPTION 'Catalog integrity writes require READ COMMITTED isolation' USING ERRCODE = '0A000';
                    END IF;
                    PERFORM pg_advisory_xact_lock(731004, 1);
                    RETURN NULL;
                END;
                $$;

                CREATE TRIGGER lock_category_integrity BEFORE INSERT OR UPDATE ON catalog.categories
                    FOR EACH STATEMENT EXECUTE FUNCTION catalog.lock_integrity_write();
                CREATE TRIGGER lock_item_integrity BEFORE INSERT OR UPDATE ON catalog.product_items
                    FOR EACH STATEMENT EXECUTE FUNCTION catalog.lock_integrity_write();

                CREATE FUNCTION catalog.check_category_cycle() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (
                        WITH RECURSIVE ancestors(id) AS (
                            SELECT parent_category_id FROM catalog.categories WHERE id = NEW.id
                            UNION
                            SELECT c.parent_category_id FROM catalog.categories c JOIN ancestors a ON c.id = a.id
                        )
                        SELECT 1 FROM ancestors WHERE id = NEW.id
                    ) THEN
                        RAISE EXCEPTION 'Category hierarchy contains a cycle' USING ERRCODE = '23514', CONSTRAINT = 'ck_category_cycle';
                    END IF;
                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER ck_category_cycle AFTER INSERT OR UPDATE ON catalog.categories
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION catalog.check_category_cycle();

                CREATE FUNCTION catalog.check_item_combination() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM catalog.product_items current_item
                        JOIN catalog.product_items other ON other.product_id = current_item.product_id
                            AND other.option_signature = current_item.option_signature AND other.id <> current_item.id
                        WHERE current_item.id = NEW.id
                    ) THEN
                        RAISE EXCEPTION 'Product option combination already exists' USING ERRCODE = '23505', CONSTRAINT = 'uq_product_item_combination';
                    END IF;
                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER uq_product_item_combination AFTER INSERT OR UPDATE ON catalog.product_items
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION catalog.check_item_combination();

                -- Validate existing data without modifying or deleting it.
                DO $$
                BEGIN
                    IF EXISTS (
                        WITH RECURSIVE ancestors(origin, id) AS (
                            SELECT id, parent_category_id FROM catalog.categories
                            UNION
                            SELECT a.origin, c.parent_category_id FROM ancestors a JOIN catalog.categories c ON c.id = a.id
                        )
                        SELECT 1 FROM ancestors WHERE origin = id
                    ) THEN
                        RAISE EXCEPTION 'Existing categories contain a cycle' USING ERRCODE = '23514';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM catalog.product_items GROUP BY product_id, option_signature HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Existing product option combinations are duplicated' USING ERRCODE = '23505';
                    END IF;
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER uq_product_item_combination ON catalog.product_items;
                DROP TRIGGER ck_category_cycle ON catalog.categories;
                DROP TRIGGER lock_item_integrity ON catalog.product_items;
                DROP TRIGGER lock_category_integrity ON catalog.categories;
                DROP FUNCTION catalog.check_item_combination();
                DROP FUNCTION catalog.check_category_cycle();
                DROP FUNCTION catalog.lock_integrity_write();
                """);
        }
    }
}