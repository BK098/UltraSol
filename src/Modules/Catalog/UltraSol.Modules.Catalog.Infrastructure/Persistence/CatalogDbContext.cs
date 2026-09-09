using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Infrastructure.Persistence;
using UltraSol.Shared.Infrastructure.Persistence.Extensions;
using UltraSol.Shared.Infrastructure.Repositories;
using CatalogCollection = UltraSol.Modules.Catalog.Domain.Catalog.Collections.Collection;

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence;

public static class Schema
{
    public const string Name = "catalog";
}
public sealed class CatalogDbContext : ModuleDbContext<CatalogDbContext>, ITransactionPreparation, IRepositoryWritePolicy
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }
    protected override bool RequireTransaction => true;
    protected override bool AllowAggregateDeletion => false;
    protected override bool RequireMaterializedAggregates => true;

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductItem> ProductItems => Set<ProductItem>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<CatalogCollection> Collections => Set<CatalogCollection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema.Name);
        CatalogModel.Configure(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }

    internal void AddAggregate<T>(T aggregate) where T : AggregateRoot
    {
        Set<T>().Add(aggregate);
        MarkMaterialized(aggregate);
    }

    protected override async Task HydrateAggregateAsync(AggregateRoot root, CancellationToken ct)
    {
        var tracking = Entry(root).State != EntityState.Detached;
        IQueryable<T> Rows<T>() where T : class => tracking ? Set<T>() : Set<T>().AsNoTracking();
        switch (root)
        {
            case Product p:
                var variations = await Rows<Variation>().Where(x => EF.Property<Guid>(x, "ProductId") == p.Id).ToListAsync(ct);
                foreach (var variation in variations)
                {
                    var options = await Rows<VariationOption>().Where(x => EF.Property<Guid>(x, "VariationId") == variation.Id).ToListAsync(ct);
                    variation.LoadOptions(options);
                }
                var media = await Rows<ProductMedia>().Where(x => EF.Property<Guid>(x, "ProductId") == p.Id).OrderBy(x => x.SortOrder).ToListAsync(ct);
                p.LoadRelated(variations, media);
                var categories = await Rows<ProductCategoryRow>().Where(x => x.ProductId == p.Id).ToListAsync(ct);
                p.LoadCategories(categories.Select(x => x.CategoryId));
                break;
            case ProductItem i:
                i.LoadMedia(await Rows<ProductItemMedia>().Where(x => EF.Property<Guid>(x, "ProductItemId") == i.Id).OrderBy(x => x.SortOrder).ToListAsync(ct));
                var selections = await Rows<SelectionRow>().Where(x => x.ProductItemId == i.Id).OrderBy(x => x.VariationId).ToListAsync(ct);
                var components = await Rows<BundleRow>().Where(x => x.BundleItemId == i.Id).OrderBy(x => x.ComponentItemId).ToListAsync(ct);
                var isBundle = await ProductItems.Where(x => x.Id == i.Id).Select(x => EF.Property<bool>(x, "StoredIsBundle")).SingleAsync(ct);
                i.LoadComposition(selections.Select(x => new OptionSelection(x.VariationId, x.OptionId)).ToArray(),
                    isBundle
                        ? BundleDefinition.FromStorage(components.Select(x => new BundleComponent(x.ComponentItemId, x.Quantity)).ToArray())
                        : null);
                break;
            case CatalogCollection c:
                var entries = await Rows<CollectionEntryRow>().Where(x => x.CollectionId == c.Id).OrderBy(x => x.SortOrder).ToListAsync(ct);
                var brands = await Rows<RuleBrandRow>().Where(x => x.CollectionId == c.Id).OrderBy(x => x.BrandId).ToListAsync(ct);
                var cats = await Rows<RuleCategoryRow>().Where(x => x.CollectionId == c.Id).OrderBy(x => x.CategoryId).ToListAsync(ct);
                var mode = await Collections.Where(x => x.Id == c.Id).Select(x => EF.Property<RuleMatchMode?>(x, "StoredMatchMode")).SingleAsync(ct);
                c.LoadMembership(entries.Select(x => new CollectionEntry(x.ProductId, x.SortOrder)),
                    c.Type == CollectionType.Automatic
                        ? CollectionRuleSet.FromStorage(mode!.Value,
                            brands.Select(x => x.BrandId).ToArray(), cats.Select(x => x.CategoryId).ToArray())
                        : null);
                break;
        }
    }

    public Task PrepareTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731004, 1)", cancellationToken);

    public void EnsureDeleteAllowed(Type entityType) =>
        throw new InvalidOperationException("Use Catalog aggregate behavior (Archive or remove child) instead of repository deletion.");
    public void EnsureBulkWriteAllowed(Type entityType) =>
        throw new InvalidOperationException("Bulk writes bypass Catalog aggregate rules and concurrency tracking.");

    protected override async Task PrepareAggregatesAsync(IReadOnlyList<AggregateRoot> roots, CancellationToken cancellationToken)
    {
        var owners = new Dictionary<object, AggregateRoot>(ReferenceEqualityComparer.Instance);
        foreach (var root in roots)
        {
            owners[root] = root;
            switch (root)
            {
                case Product p:
                    foreach (var v in p.Variations) { owners[v] = p; foreach (var o in v.Options) owners[o] = p; }
                    foreach (var media in p.Media) owners[media] = p;
                    Sync(p, Set<ProductCategoryRow>().Local.Where(x => x.ProductId == p.Id),
                        p.CategoryIds.Select(id => new ProductCategoryRow { ProductId = p.Id, CategoryId = id }), x => x.CategoryId, owners);
                    break;
                case ProductItem i:
                    foreach (var media in i.Media) owners[media] = i;
                    Entry(i).Property<bool>("StoredIsBundle").CurrentValue = i.IsBundle;
                    Sync(i, Set<SelectionRow>().Local.Where(x => x.ProductItemId == i.Id),
                        i.OptionSelections.Select(x => new SelectionRow { ProductItemId = i.Id, ProductId = i.ProductId, VariationId = x.VariationId, OptionId = x.OptionId }), x => x.VariationId, owners);
                    Sync(i, Set<BundleRow>().Local.Where(x => x.BundleItemId == i.Id),
                        (i.BundleDefinition?.Components ?? []).Select(x => new BundleRow { BundleItemId = i.Id, ComponentItemId = x.ProductItemId, Quantity = x.Quantity }), x => x.ComponentItemId, owners);
                    break;
                case CatalogCollection c:
                    Entry(c).Property<RuleMatchMode?>("StoredMatchMode").CurrentValue = c.Rules?.MatchMode;
                    Sync(c, Set<CollectionEntryRow>().Local.Where(x => x.CollectionId == c.Id),
                        c.Entries.Select(x => new CollectionEntryRow { CollectionId = c.Id, ProductId = x.ProductId, SortOrder = x.SortOrder }), x => x.ProductId, owners);
                    Sync(c, Set<RuleBrandRow>().Local.Where(x => x.CollectionId == c.Id),
                        (c.Rules?.BrandIds ?? []).Select(id => new RuleBrandRow { CollectionId = c.Id, BrandId = id }), x => x.BrandId, owners);
                    Sync(c, Set<RuleCategoryRow>().Local.Where(x => x.CollectionId == c.Id),
                        (c.Rules?.CategoryIds ?? []).Select(id => new RuleCategoryRow { CollectionId = c.Id, CategoryId = id }), x => x.CategoryId, owners);
                    break;
            }
        }
        ChangeTracker.DetectChanges();
        var dirty = new HashSet<AggregateRoot>();
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            if (owners.TryGetValue(entry.Entity, out var root))
            {
                dirty.Add(root);
            }
            else if (entry.Entity is ProductMedia or Variation)
            {
                AddOwner<Product>("ProductId", entry);
            }
            else if (entry.Entity is ProductItemMedia)
            {
                AddOwner<ProductItem>("ProductItemId", entry);
            }
            else if (entry.Entity is VariationOption)
            {
                var id = entry.Property("VariationId").CurrentValue;
                var variation = ChangeTracker.Entries<Variation>().First(x => Equals(x.Entity.Id, id));
                AddOwner<Product>("ProductId", variation);
            }
        }
        void AddOwner<T>(string fk, Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry child) where T : AggregateRoot
        {
            var id = (Guid)child.Property(fk).CurrentValue!;
            var root = roots.OfType<T>().SingleOrDefault(x => x.Id == id)
                ?? throw new InvalidOperationException("The owner aggregate must be loaded.");
            dirty.Add(root);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var root in dirty)
        {
            if (Entry(root).State == EntityState.Added)
            {
                root.MarkCreated(root.CreatedBy, now);
            }
            else
            {
                root.MarkUpdated(root.UpdatedBy, now);
            }
        }

        // Clear previous primary flags before EF sets new ones (partial unique indexes are immediate).
        foreach (var p in dirty.OfType<Product>())
        {
            var primary = p.Media.SingleOrDefault(x => x.IsPrimary);
            if (primary is not null)
            {
                await Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE catalog.product_media SET is_primary = false WHERE product_id = {p.Id} AND id <> {primary.Id} AND is_primary", cancellationToken);
            }
        }
        foreach (var i in dirty.OfType<ProductItem>())
        {
            var primary = i.Media.SingleOrDefault(x => x.IsPrimary);
            if (primary is not null)
            {
                await Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE catalog.product_item_media SET is_primary = false WHERE product_item_id = {i.Id} AND id <> {primary.Id} AND is_primary", cancellationToken);
            }
        }
    }

    private void Sync<T>(AggregateRoot root, IEnumerable<T> current, IEnumerable<T> desired,
        Func<T, Guid> key, Dictionary<object, AggregateRoot> owners) where T : class
    {
        var old = current.ToDictionary(key);
        foreach (var row in desired)
        {
            if (old.Remove(key(row), out var existing))
            {
                Entry(existing).CurrentValues.SetValues(row);
                owners[existing] = root;
            }
            else
            {
                Set<T>().Add(row);
                owners[row] = root;
            }
        }
        foreach (var row in old.Values) { Set<T>().Remove(row); owners[row] = root; }
    }
}