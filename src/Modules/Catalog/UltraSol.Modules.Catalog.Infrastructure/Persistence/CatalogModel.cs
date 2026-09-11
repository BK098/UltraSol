using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using CatalogCollection = UltraSol.Modules.Catalog.Domain.Catalog.Collections.Collection;
using UltraSol.Shared.Infrastructure.Persistence.Extensions;

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence;

internal static class CatalogModel
{
    internal static void Configure(ModelBuilder m)
    {
        var product = ModelConfigure.Root<Product>(m, "products");
        product.Ignore(x => x.CategoryIds);
        product.Property(x => x.Name).IsRequired();
        product.HasIndex(x => new { x.Status, x.Id });
        product.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        product.HasMany(x => x.Variations).WithOne().HasForeignKey("ProductId").OnDelete(DeleteBehavior.Cascade);
        product.Navigation(x => x.Variations).HasField("_variations").UsePropertyAccessMode(PropertyAccessMode.Field);
        product.HasMany(x => x.Media).WithOne().HasForeignKey("ProductId").OnDelete(DeleteBehavior.Cascade);
        product.Navigation(x => x.Media).HasField("_media").UsePropertyAccessMode(PropertyAccessMode.Field);
        product.ToTable("products", t => t.HasCheckConstraint("ck_product_status", "status IN (1,2,3,4)"));

        var variation = ModelConfigure.Entity<Variation>(m, "variations");
        variation.Property<Guid>("ProductId");
        variation.HasAlternateKey("Id", "ProductId");
        variation.HasMany(x => x.Options).WithOne().HasForeignKey("VariationId").OnDelete(DeleteBehavior.Cascade);
        variation.Navigation(x => x.Options).HasField("_options").UsePropertyAccessMode(PropertyAccessMode.Field);
        var option = ModelConfigure.Entity<VariationOption>(m, "variation_options");
        option.Property<Guid>("VariationId");
        option.HasAlternateKey("Id", "VariationId");
        ModelConfigure.Media<ProductMedia>(m, "product_media", "ProductId");

        var item = ModelConfigure.Root<ProductItem>(m, "product_items");
        item.Ignore(x => x.OptionSelections);
        item.Ignore(x => x.BundleDefinition);
        item.Ignore(x => x.IsBundle);
        item.Property<bool>("StoredIsBundle").HasColumnName("is_bundle");
        item.Property(x => x.ProductId);
        item.HasAlternateKey(x => new { x.Id, x.ProductId });
        item.Property(x => x.Sku).HasConversion(x => x.Value, x => SKU.Create(x)).HasColumnName("sku").HasMaxLength(64).IsRequired();
        item.Property(x => x.Status).HasColumnName("status").HasDefaultValue(ProductItemStatus.Draft).IsRequired();
        item.Property(x => x.OptionSignature).HasConversion(x => x.Value, x => OptionSignature.FromStorage(x)).IsRequired();
        item.HasIndex(x => x.Sku).IsUnique();
        item.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        item.HasMany(x => x.Media).WithOne().HasForeignKey("ProductItemId").OnDelete(DeleteBehavior.Cascade);
        item.Navigation(x => x.Media).HasField("_media").UsePropertyAccessMode(PropertyAccessMode.Field);
        ModelConfigure.Media<ProductItemMedia>(m, "product_item_media", "ProductItemId");

        var brand = ModelConfigure.Root<Brand>(m, "brands");
        brand.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();
        var category = ModelConfigure.Root<Category>(m, "categories");
        category.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        category.ToTable("categories", t => t.HasCheckConstraint("ck_category_parent", "parent_category_id IS NULL OR parent_category_id <> id"));

        var collection = ModelConfigure.Root<CatalogCollection>(m, "collections");
        collection.Ignore(x => x.Entries);
        collection.Ignore(x => x.Rules);
        collection.Property(x => x.Type);
        collection.Property<RuleMatchMode?>("StoredMatchMode").HasColumnName("match_mode");
        collection.ToTable("collections", t => t.HasCheckConstraint("ck_collection_type",
            "(type = 1 AND match_mode IS NULL) OR (type = 2 AND match_mode IS NOT NULL AND match_mode IN (1,2))"));

        var pc = m.Entity<ProductCategoryRow>();
        pc.ToTable("product_categories"); pc.HasKey(x => new { x.ProductId, x.CategoryId });
        pc.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        pc.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        pc.HasIndex(x => new { x.CategoryId, x.ProductId });

        var selection = m.Entity<SelectionRow>();
        selection.ToTable("product_item_selections");
        selection.HasKey(x => new { x.ProductItemId, x.VariationId });
        selection.HasOne<ProductItem>().WithMany().HasForeignKey(x => new { x.ProductItemId, x.ProductId })
            .HasPrincipalKey(x => new { x.Id, x.ProductId }).OnDelete(DeleteBehavior.Cascade);
        selection.HasOne<Variation>().WithMany().HasForeignKey(x => new { x.VariationId, x.ProductId })
            .HasPrincipalKey("Id", "ProductId").OnDelete(DeleteBehavior.Restrict);
        selection.HasOne<VariationOption>().WithMany().HasForeignKey(x => new { x.OptionId, x.VariationId })
            .HasPrincipalKey("Id", "VariationId").OnDelete(DeleteBehavior.Restrict);

        var bundle = m.Entity<BundleRow>();
        bundle.ToTable("bundle_components", t => t.HasCheckConstraint("ck_bundle_component",
            "quantity > 0 AND bundle_item_id <> component_item_id"));
        bundle.HasKey(x => new { x.BundleItemId, x.ComponentItemId });
        bundle.HasOne<ProductItem>().WithMany().HasForeignKey(x => x.BundleItemId).OnDelete(DeleteBehavior.Cascade);
        bundle.HasOne<ProductItem>().WithMany().HasForeignKey(x => x.ComponentItemId).OnDelete(DeleteBehavior.Restrict);

        var entry = m.Entity<CollectionEntryRow>();
        entry.ToTable("collection_entries", t => t.HasCheckConstraint("ck_collection_order", "sort_order >= 0"));
        entry.HasKey(x => new { x.CollectionId, x.ProductId });
        entry.HasOne<CatalogCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
        entry.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        entry.HasIndex(x => new { x.CollectionId, x.SortOrder });
        entry.HasIndex(x => new { x.ProductId, x.CollectionId });

        var rb = m.Entity<RuleBrandRow>();
        rb.ToTable("collection_rule_brands"); rb.HasKey(x => new { x.CollectionId, x.BrandId });
        rb.HasOne<CatalogCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
        rb.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        var rc = m.Entity<RuleCategoryRow>();
        rc.ToTable("collection_rule_categories"); rc.HasKey(x => new { x.CollectionId, x.CategoryId });
        rc.HasOne<CatalogCollection>().WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
        rc.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);

        foreach (var entity in m.Model.GetEntityTypes())
        foreach (var p in entity.GetProperties())
        {
            if (p.Name is not ("StoredIsBundle" or "StoredMatchMode"))
            {
                p.SetColumnName(ModelConfigure.Snake(p.Name));
            }
        }
    }
}
