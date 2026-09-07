using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CatalogDbContext))]
[Migration("20260907081200_CatalogIntegrity")]
public sealed class CatalogIntegrity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        -- All writers use the same transaction lock before inspecting Catalog state.
        CREATE FUNCTION catalog.lock_write() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            PERFORM pg_advisory_xact_lock(731004, 1);
            RETURN NULL;
        END $$;

        DO $$
        DECLARE t text;
        BEGIN
            FOREACH t IN ARRAY ARRAY['products','product_items','brands','categories','collections',
                'variations','variation_options','product_categories','product_media','product_item_media',
                'product_item_selections','bundle_components','collection_entries','collection_rule_brands','collection_rule_categories']
            LOOP
                EXECUTE format('CREATE TRIGGER catalog_write_lock BEFORE INSERT OR UPDATE OR DELETE ON catalog.%I FOR EACH STATEMENT EXECUTE FUNCTION catalog.lock_write()', t);
            END LOOP;
        END $$;

        CREATE FUNCTION catalog.check_product() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE pid uuid; row_data jsonb; iid uuid;
        BEGIN
            row_data := CASE WHEN TG_OP = 'DELETE' THEN to_jsonb(OLD) ELSE to_jsonb(NEW) END;
            IF TG_TABLE_NAME = 'products' THEN pid := (row_data->>'id')::uuid;
            ELSIF TG_TABLE_NAME = 'variation_options' THEN
                SELECT product_id INTO pid FROM catalog.variations WHERE id = (row_data->>'variation_id')::uuid;
            ELSE pid := (row_data->>'product_id')::uuid;
            END IF;
            IF pid IS NULL OR NOT EXISTS (SELECT 1 FROM catalog.products WHERE id = pid) THEN RETURN NULL; END IF;
            PERFORM 1 FROM catalog.products WHERE id = pid FOR UPDATE;
            IF EXISTS (
                SELECT 1 FROM catalog.product_items i WHERE i.product_id = pid AND (
                    (SELECT count(*) FROM catalog.product_item_selections s WHERE s.product_item_id = i.id)
                        <> (SELECT count(*) FROM catalog.variations v WHERE v.product_id = pid)
                    OR i.option_signature <> COALESCE((SELECT string_agg(replace(s.variation_id::text,'-','') || ':' ||
                        replace(s.option_id::text,'-',''), '|' ORDER BY s.variation_id)
                        FROM catalog.product_item_selections s WHERE s.product_item_id = i.id), '')
                )
            ) THEN RAISE EXCEPTION 'Catalog item selections/signature are inconsistent' USING ERRCODE = '23514'; END IF;
            IF EXISTS (
                SELECT 1 FROM catalog.product_items WHERE product_id = pid
                GROUP BY option_signature HAVING count(*) > 1
            ) THEN RAISE EXCEPTION 'Catalog product option combination already exists' USING ERRCODE = '23505'; END IF;
            IF EXISTS (
                SELECT 1 FROM catalog.products p WHERE p.id = pid AND
                    ((p.is_structure_locked AND NOT EXISTS (SELECT 1 FROM catalog.product_items i WHERE i.product_id = pid))
                    OR (NOT p.is_structure_locked AND EXISTS (SELECT 1 FROM catalog.product_items i WHERE i.product_id = pid))
                    OR (p.status IN (2,3) AND NOT p.is_structure_locked))
            ) THEN RAISE EXCEPTION 'Catalog product structure lock is inconsistent' USING ERRCODE = '23514'; END IF;
            RETURN NULL;
        END $$;

        DO $$
        DECLARE t text;
        BEGIN
            FOREACH t IN ARRAY ARRAY['products','product_items','variations','variation_options','product_item_selections']
            LOOP
                EXECUTE format('CREATE CONSTRAINT TRIGGER catalog_product_integrity AFTER INSERT OR UPDATE OR DELETE ON catalog.%I DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION catalog.check_product()', t);
            END LOOP;
        END $$;

        CREATE FUNCTION catalog.check_bundle() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE iid uuid; data jsonb;
        BEGIN
            data := CASE WHEN TG_OP = 'DELETE' THEN to_jsonb(OLD) ELSE to_jsonb(NEW) END;
            iid := CASE WHEN TG_TABLE_NAME = 'product_items' THEN (data->>'id')::uuid ELSE (data->>'bundle_item_id')::uuid END;
            IF EXISTS (
                SELECT 1 FROM catalog.product_items i WHERE i.id = iid AND
                    (i.is_bundle <> EXISTS (SELECT 1 FROM catalog.bundle_components b WHERE b.bundle_item_id = i.id))
            ) OR EXISTS (
                SELECT 1 FROM catalog.bundle_components b JOIN catalog.product_items i ON i.id = b.component_item_id
                WHERE (b.bundle_item_id = iid OR b.component_item_id = iid) AND i.is_bundle
            ) THEN RAISE EXCEPTION 'Catalog bundle must contain ordinary items only and cannot be empty' USING ERRCODE = '23514'; END IF;
            RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER catalog_bundle_integrity AFTER INSERT OR UPDATE OR DELETE ON catalog.bundle_components
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION catalog.check_bundle();
        CREATE CONSTRAINT TRIGGER catalog_bundle_integrity AFTER INSERT OR UPDATE ON catalog.product_items
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION catalog.check_bundle();

        CREATE FUNCTION catalog.check_collection() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE cid uuid; data jsonb;
        BEGIN
            data := CASE WHEN TG_OP = 'DELETE' THEN to_jsonb(OLD) ELSE to_jsonb(NEW) END;
            cid := CASE WHEN TG_TABLE_NAME = 'collections' THEN (data->>'id')::uuid ELSE (data->>'collection_id')::uuid END;
            IF EXISTS (
                SELECT 1 FROM catalog.collections c WHERE c.id = cid AND (
                    (c.type = 1 AND (EXISTS(SELECT 1 FROM catalog.collection_rule_brands WHERE collection_id = cid)
                        OR EXISTS(SELECT 1 FROM catalog.collection_rule_categories WHERE collection_id = cid)))
                    OR (c.type = 2 AND (EXISTS(SELECT 1 FROM catalog.collection_entries WHERE collection_id = cid)
                        OR NOT (EXISTS(SELECT 1 FROM catalog.collection_rule_brands WHERE collection_id = cid)
                            OR EXISTS(SELECT 1 FROM catalog.collection_rule_categories WHERE collection_id = cid))))
                )
            ) THEN RAISE EXCEPTION 'Catalog collection membership/rules conflict with its type' USING ERRCODE = '23514'; END IF;
            RETURN NULL;
        END $$;
        DO $$
        DECLARE t text;
        BEGIN
            FOREACH t IN ARRAY ARRAY['collections','collection_entries','collection_rule_brands','collection_rule_categories']
            LOOP
                EXECUTE format('CREATE CONSTRAINT TRIGGER catalog_collection_integrity AFTER INSERT OR UPDATE OR DELETE ON catalog.%I DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION catalog.check_collection()', t);
            END LOOP;
        END $$;

        CREATE FUNCTION catalog.check_category() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE current_id uuid; visited uuid[] := ARRAY[NEW.id]; archived boolean;
        BEGIN
            SELECT parent_category_id INTO current_id FROM catalog.categories WHERE id = NEW.id;
            WHILE current_id IS NOT NULL LOOP
                IF current_id = ANY(visited) THEN RAISE EXCEPTION 'Catalog category cycle' USING ERRCODE = '23514'; END IF;
                visited := array_append(visited, current_id);
                SELECT parent_category_id INTO current_id FROM catalog.categories WHERE id = current_id;
            END LOOP;
            IF EXISTS (
                SELECT 1 FROM catalog.categories p JOIN catalog.categories child ON child.parent_category_id = p.id
                WHERE p.id = NEW.id AND p.is_archived AND NOT child.is_archived
            ) THEN RAISE EXCEPTION 'Catalog archived category has active children' USING ERRCODE = '23514'; END IF;
            RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER catalog_category_integrity AFTER INSERT OR UPDATE ON catalog.categories
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION catalog.check_category();

        CREATE FUNCTION catalog.check_reference() RETURNS trigger LANGUAGE plpgsql AS $$
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
                AND EXISTS(SELECT 1 FROM catalog.categories WHERE id = NEW.parent_category_id AND is_archived)
                THEN RAISE EXCEPTION 'Cannot assign archived parent' USING ERRCODE = '23514'; END IF;
            RETURN NEW;
        END $$;
        DO $$
        DECLARE t text;
        BEGIN
            FOREACH t IN ARRAY ARRAY['products','categories','product_categories','collection_rule_brands','collection_rule_categories']
            LOOP
                EXECUTE format('CREATE TRIGGER catalog_reference_integrity BEFORE INSERT OR UPDATE ON catalog.%I FOR EACH ROW EXECUTE FUNCTION catalog.check_reference()', t);
            END LOOP;
        END $$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP FUNCTION catalog.check_reference() CASCADE;
        DROP FUNCTION catalog.check_category() CASCADE;
        DROP FUNCTION catalog.check_collection() CASCADE;
        DROP FUNCTION catalog.check_bundle() CASCADE;
        DROP FUNCTION catalog.check_product() CASCADE;
        DROP FUNCTION catalog.lock_write() CASCADE;
        """);
}

