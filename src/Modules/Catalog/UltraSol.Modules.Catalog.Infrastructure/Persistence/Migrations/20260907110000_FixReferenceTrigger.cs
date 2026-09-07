using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CatalogDbContext))]
[Migration("20260907110000_FixReferenceTrigger")]
public sealed class FixReferenceTrigger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION catalog.check_reference() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE data jsonb := to_jsonb(NEW); old_data jsonb;
        BEGIN
            IF TG_OP = 'UPDATE' THEN old_data := to_jsonb(OLD); ELSE old_data := '{}'::jsonb; END IF;
            IF TG_TABLE_NAME IN ('products','collection_rule_brands') AND data->>'brand_id' IS DISTINCT FROM old_data->>'brand_id'
                AND EXISTS(SELECT 1 FROM catalog.brands WHERE id = (data->>'brand_id')::uuid AND is_archived)
                THEN RAISE EXCEPTION 'Cannot assign archived brand' USING ERRCODE = '23514'; END IF;
            IF TG_TABLE_NAME IN ('product_categories','collection_rule_categories') AND data->>'category_id' IS DISTINCT FROM old_data->>'category_id'
                AND EXISTS(SELECT 1 FROM catalog.categories WHERE id = (data->>'category_id')::uuid AND is_archived)
                THEN RAISE EXCEPTION 'Cannot assign archived category' USING ERRCODE = '23514'; END IF;
            IF TG_TABLE_NAME = 'categories' AND data->>'parent_category_id' IS DISTINCT FROM old_data->>'parent_category_id'
                AND EXISTS(SELECT 1 FROM catalog.categories WHERE id = (data->>'parent_category_id')::uuid AND is_archived)
                THEN RAISE EXCEPTION 'Cannot assign archived parent' USING ERRCODE = '23514'; END IF;
            RETURN NEW;
        END $$;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) { /* Keep the bug fix when rolling back this metadata-only migration. */ }
}

