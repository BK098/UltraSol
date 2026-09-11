using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;

namespace UltraSol.Modules.Catalog.Infrastructure.Reads;

public sealed class CatalogReadStore(CatalogDbContext db) : ICatalogReadStore
{
    private static string? Search(PaginationRequest page) => string.IsNullOrWhiteSpace(page.Search) ? null : page.Search.Trim();

    private static async Task<PaginatedResult<T>> Page<T>(IQueryable<T> query, PaginationRequest page, CancellationToken ct)
    {
        var count = await query.CountAsync(ct);
        var rows = await query.Skip(page.Skip).Take(page.Take).ToListAsync(ct);
        return PaginatedResult<T>.Create(rows, count, page);
    }

    private IQueryable<ProductData> ProductRows(IQueryable<Product> products, bool detail = false) =>
        from p in products
        join b in db.Brands.AsNoTracking() on p.BrandId equals b.Id into brands
        from b in brands.DefaultIfEmpty()
        select new ProductData(p.Id, p.Name, detail ? p.Description : null, p.Status,
            b == null ? null : new NamedData(b.Id, b.Name),
            p.Media.Where(m => m.IsPrimary).Select(m => m.Url).FirstOrDefault(),
            Array.Empty<NamedData>(), Array.Empty<MediaData>(), Array.Empty<VariationData>());

    public async Task<PaginatedResult<ProductData>> ProductsAsync(PaginationRequest page, ProductReadFilter filter, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        if (filter.BrandId is { } brandId)
        {
            await BrandAsync(brandId, ct);
            query = query.Where(p => p.BrandId == brandId);
        }
        if (filter.CategoryId is { } categoryId)
        {
            await CategoryAsync(categoryId, ct);
            query = query.Where(p => db.Set<ProductCategoryRow>().Any(r => r.ProductId == p.Id && r.CategoryId == categoryId));
        }
        var manual = false;
        if (filter.CollectionId is { } collectionId)
        {
            var collection = await CollectionAsync(collectionId, ct);
            manual = collection.Type == CollectionType.Manual;
            if (manual)
            {
                query = query.Where(p => db.Set<CollectionEntryRow>().Any(r => r.CollectionId == collectionId && r.ProductId == p.Id));
            }
            else
            {
                var brandIds = collection.Brands.Select(x => x.Id).ToArray();
                var categoryIds = collection.Categories.Select(x => x.Id).ToArray();
                if (collection.MatchMode == RuleMatchMode.All)
                {
                    query = query.Where(p => (brandIds.Length == 0 || (p.BrandId.HasValue && brandIds.Contains(p.BrandId.Value))) &&
                        (categoryIds.Length == 0 || db.Set<ProductCategoryRow>().Any(r => r.ProductId == p.Id && categoryIds.Contains(r.CategoryId))));
                }
                else
                {
                    query = query.Where(p => (p.BrandId.HasValue && brandIds.Contains(p.BrandId.Value)) ||
                        db.Set<ProductCategoryRow>().Any(r => r.ProductId == p.Id && categoryIds.Contains(r.CategoryId)));
                }
            }
        }
        var search = Search(page);
        if (search is not null)
        {
            query = query.Where(p => p.Name.ToLower().Contains(search.ToLower()));
        }
        if (filter.Status.HasValue)
        {
            query = query.Where(p => p.Status == filter.Status.Value);
        }
        var ordered = manual
            ? query.OrderBy(p => db.Set<CollectionEntryRow>().Where(r => r.CollectionId == filter.CollectionId && r.ProductId == p.Id).Select(r => r.SortOrder).First()).ThenBy(p => p.Id)
            : query.OrderBy(p => p.Name).ThenBy(p => p.Id);
        var result = await Page(ProductRows(ordered), page, ct);
        var categories = await ProductCategories(result.Items.Select(x => x.Id).ToArray(), ct);
        return result.Map(p => p with { Categories = categories[p.Id].ToArray() });
    }

    private async Task<ILookup<Guid, NamedData>> ProductCategories(Guid[] productIds, CancellationToken ct)
    {
        var rows = await (from r in db.Set<ProductCategoryRow>().AsNoTracking()
                          join c in db.Categories.AsNoTracking() on r.CategoryId equals c.Id
                          where productIds.Contains(r.ProductId)
                          orderby c.Name, c.Id
                          select new { r.ProductId, Category = new NamedData(c.Id, c.Name) }).ToListAsync(ct);
        return rows.ToLookup(x => x.ProductId, x => x.Category);
    }

    public async Task<ProductData> ProductAsync(Guid id, CancellationToken ct)
    {
        var product = await ProductRows(db.Products.AsNoTracking().Where(p => p.Id == id), detail: true).SingleOrDefaultAsync(ct)
            ?? throw EntityNotFoundException.For<Product>(id);
        var categories = await ProductCategories([id], ct);
        var media = await db.Set<ProductMedia>().AsNoTracking().Where(m => EF.Property<Guid>(m, "ProductId") == id)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Id)
            .Select(m => new MediaData(m.Id, m.Url, m.AltText, m.SortOrder, m.IsPrimary)).ToListAsync(ct);
        var variations = await VariationRows(id).OrderBy(v => v.Name).ThenBy(v => v.Id).ToListAsync(ct);
        var options = await Options(variations.Select(v => v.Id).ToArray(), ct);
        return product with
        {
            Categories = categories[id].ToArray(),
            Media = media,
            Variations = variations.Select(v => v with { Options = options[v.Id].ToArray() }).ToArray()
        };
    }

    private IQueryable<ItemData> ItemRows(IQueryable<ProductItem> items) =>
        from i in items
        join p in db.Products.AsNoTracking() on i.ProductId equals p.Id
        select new ItemData(i.Id, i.Sku.Value, i.Status, new NamedData(p.Id, p.Name), EF.Property<bool>(i, "StoredIsBundle"),
            i.Media.Where(m => m.IsPrimary).Select(m => m.Url).FirstOrDefault(),
            Array.Empty<SelectionData>(), Array.Empty<MediaData>(), Array.Empty<ComponentData>());

    private async Task<ILookup<Guid, SelectionData>> Selections(Guid[] itemIds, CancellationToken ct)
    {
        var rows = await (from s in db.Set<SelectionRow>().AsNoTracking()
                          join v in db.Set<Variation>().AsNoTracking() on s.VariationId equals v.Id
                          join o in db.Set<VariationOption>().AsNoTracking() on s.OptionId equals o.Id
                          where itemIds.Contains(s.ProductItemId)
                          orderby v.Name, v.Id
                          select new { s.ProductItemId, Selection = new SelectionData(v.Id, v.Name, o.Id, o.Value) }).ToListAsync(ct);
        return rows.ToLookup(x => x.ProductItemId, x => x.Selection);
    }

    public async Task<PaginatedResult<ItemData>> ItemsAsync(PaginationRequest page, ItemReadFilter filter, CancellationToken ct)
    {
        var search = Search(page)?.ToUpperInvariant();
        // SKU is a converted value object; search its stored text with a parameterized SQL source.
        var query = search is null
            ? db.ProductItems.AsNoTracking()
            : db.ProductItems.FromSqlInterpolated($"SELECT * FROM catalog.product_items WHERE strpos(sku, {search}) > 0").AsNoTracking();
        if (filter.ProductId is { } productId)
        {
            await RequireProduct(productId, ct);
            query = query.Where(i => i.ProductId == productId);
        }
        if (filter.Status.HasValue)
        {
            query = query.Where(i => i.Status == filter.Status.Value);
        }
        if (filter.IsBundle.HasValue)
        {
            query = query.Where(i => EF.Property<bool>(i, "StoredIsBundle") == filter.IsBundle.Value);
        }
        var result = await Page(ItemRows(query.OrderBy(i => i.Sku).ThenBy(i => i.Id)), page, ct);
        var selections = await Selections(result.Items.Select(i => i.Id).ToArray(), ct);
        return result.Map(i => i with { Selections = selections[i.Id].ToArray() });
    }

    public async Task<ItemData> ItemAsync(Guid id, Guid? productId, bool requireBundle, CancellationToken ct)
    {
        var query = db.ProductItems.AsNoTracking().Where(i => i.Id == id);
        if (productId.HasValue)
        {
            await RequireProduct(productId.Value, ct);
            query = query.Where(i => i.ProductId == productId.Value);
        }
        if (requireBundle)
        {
            query = query.Where(i => EF.Property<bool>(i, "StoredIsBundle"));
        }
        var item = await ItemRows(query).SingleOrDefaultAsync(ct) ?? throw EntityNotFoundException.For<ProductItem>(id);
        var selections = await Selections([id], ct);
        var media = await db.Set<ProductItemMedia>().AsNoTracking().Where(m => EF.Property<Guid>(m, "ProductItemId") == id)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Id)
            .Select(m => new MediaData(m.Id, m.Url, m.AltText, m.SortOrder, m.IsPrimary)).ToListAsync(ct);
        var components = await (from r in db.Set<BundleRow>().AsNoTracking()
                                join i in db.ProductItems.AsNoTracking() on r.ComponentItemId equals i.Id
                                join p in db.Products.AsNoTracking() on i.ProductId equals p.Id
                                where r.BundleItemId == id
                                orderby i.Sku, i.Id
                                select new { i.Id, i.Sku, i.Status, Product = new NamedData(p.Id, p.Name), r.Quantity,
                                    PrimaryMediaUrl = i.Media.Where(m => m.IsPrimary).Select(m => m.Url).FirstOrDefault() }).ToListAsync(ct);
        return item with
        {
            Selections = selections[id].ToArray(),
            Media = media,
            Components = components.Select(c => new ComponentData(c.Id, c.Sku.Value, c.Product, c.Status.ToString(), c.Quantity, c.PrimaryMediaUrl)).ToArray()
        };
    }

    private IQueryable<BrandData> BrandRows() => db.Brands.AsNoTracking()
        .Select(b => new BrandData { Id = b.Id, Name = b.Name, Description = b.Description, LogoUrl = b.LogoUrl, IsActive = b.IsActive, IsArchived = b.IsArchived });

    public Task<PaginatedResult<BrandData>> BrandsAsync(PaginationRequest page, bool? isActive, bool? isArchived, CancellationToken ct)
    {
        var query = BrandRows();
        var search = Search(page);
        if (search is not null)
        {
            query = query.Where(b => b.Name.ToLower().Contains(search.ToLower()));
        }
        if (isActive.HasValue)
        {
            query = query.Where(b => b.IsActive == isActive.Value);
        }
        if (isArchived.HasValue)
        {
            query = query.Where(b => b.IsArchived == isArchived.Value);
        }
        return Page(query.OrderBy(b => b.Name).ThenBy(b => b.Id), page, ct);
    }

    public async Task<BrandData> BrandAsync(Guid id, CancellationToken ct) =>
        await BrandRows().SingleOrDefaultAsync(b => b.Id == id, ct) ?? throw EntityNotFoundException.For<Brand>(id);

    private IQueryable<CategoryData> CategoryRows() =>
        from c in db.Categories.AsNoTracking()
        join p in db.Categories.AsNoTracking() on c.ParentCategoryId equals p.Id into parents
        from p in parents.DefaultIfEmpty()
        select new CategoryData { Id = c.Id, Name = c.Name, Description = c.Description, IsArchived = c.IsArchived,
            Parent = p == null ? null : new NamedData(p.Id, p.Name) };

    public async Task<PaginatedResult<CategoryData>> CategoriesAsync(PaginationRequest page, Guid? parentId, bool rootsOnly, bool? isArchived, CancellationToken ct)
    {
        var entities = db.Categories.AsNoTracking();
        if (parentId.HasValue)
        {
            await CategoryAsync(parentId.Value, ct);
            entities = entities.Where(c => c.ParentCategoryId == parentId.Value);
        }
        if (rootsOnly)
        {
            entities = entities.Where(c => c.ParentCategoryId == null);
        }
        if (isArchived.HasValue)
        {
            entities = entities.Where(c => c.IsArchived == isArchived.Value);
        }
        var search = Search(page);
        if (search is not null)
        {
            entities = entities.Where(c => c.Name.ToLower().Contains(search.ToLower()));
        }
        var query = from c in entities
                    join p in db.Categories.AsNoTracking() on c.ParentCategoryId equals p.Id into parents
                    from p in parents.DefaultIfEmpty()
                    orderby c.Name, c.Id
                    select new CategoryData { Id = c.Id, Name = c.Name, Description = c.Description, IsArchived = c.IsArchived,
                        Parent = p == null ? null : new NamedData(p.Id, p.Name) };
        return await Page(query, page, ct);
    }

    public async Task<CategoryData> CategoryAsync(Guid id, CancellationToken ct) =>
        await CategoryRows().SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw EntityNotFoundException.For<Category>(id);

    private IQueryable<CollectionData> CollectionRows() => db.Collections.AsNoTracking()
        .Select(c => new CollectionData { Id = c.Id, Name = c.Name, Description = c.Description, Type = c.Type, Status = c.Status,
            MatchMode = EF.Property<RuleMatchMode?>(c, "StoredMatchMode"), Brands = Array.Empty<NamedData>(), Categories = Array.Empty<NamedData>() });

    public Task<PaginatedResult<CollectionData>> CollectionsAsync(PaginationRequest page, CollectionStatus? status, CollectionType? type, CancellationToken ct)
    {
        var query = CollectionRows();
        var search = Search(page);
        if (search is not null)
        {
            query = query.Where(c => c.Name.ToLower().Contains(search.ToLower()));
        }
        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }
        if (type.HasValue)
        {
            query = query.Where(c => c.Type == type.Value);
        }
        return Page(query.OrderBy(c => c.Name).ThenBy(c => c.Id), page, ct);
    }

    public async Task<CollectionData> CollectionAsync(Guid id, CancellationToken ct)
    {
        var collection = await CollectionRows().SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw EntityNotFoundException.For<Collection>(id);
        var brands = await (from r in db.Set<RuleBrandRow>().AsNoTracking()
                            join b in db.Brands.AsNoTracking() on r.BrandId equals b.Id
                            where r.CollectionId == id
                            orderby b.Name, b.Id
                            select new NamedData(b.Id, b.Name)).ToListAsync(ct);
        var categories = await (from r in db.Set<RuleCategoryRow>().AsNoTracking()
                                join c in db.Categories.AsNoTracking() on r.CategoryId equals c.Id
                                where r.CollectionId == id
                                orderby c.Name, c.Id
                                select new NamedData(c.Id, c.Name)).ToListAsync(ct);
        return collection with { Brands = brands, Categories = categories };
    }

    private async Task RequireProduct(Guid id, CancellationToken ct)
    {
        if (!await db.Products.AsNoTracking().AnyAsync(p => p.Id == id, ct))
        {
            throw EntityNotFoundException.For<Product>(id);
        }
    }

    private IQueryable<VariationData> VariationRows(Guid productId) =>
        from v in db.Set<Variation>().AsNoTracking()
        join p in db.Products.AsNoTracking() on EF.Property<Guid>(v, "ProductId") equals p.Id
        where p.Id == productId
        select new VariationData { Id = v.Id, Name = v.Name, Product = new NamedData(p.Id, p.Name), OptionCount = v.Options.Count, Options = Array.Empty<OptionData>() };

    private async Task<ILookup<Guid, OptionData>> Options(Guid[] variationIds, CancellationToken ct)
    {
        var rows = await db.Set<VariationOption>().AsNoTracking().Where(o => variationIds.Contains(EF.Property<Guid>(o, "VariationId")))
            .OrderBy(o => o.Value).ThenBy(o => o.Id)
            .Select(o => new { VariationId = EF.Property<Guid>(o, "VariationId"), Option = new OptionData(o.Id, o.Value) }).ToListAsync(ct);
        return rows.ToLookup(x => x.VariationId, x => x.Option);
    }

    public async Task<PaginatedResult<VariationData>> VariationsAsync(Guid productId, PaginationRequest page, CancellationToken ct)
    {
        await RequireProduct(productId, ct);
        var query = VariationRows(productId);
        var search = Search(page);
        if (search is not null)
        {
            query = query.Where(v => v.Name.ToLower().Contains(search.ToLower()));
        }
        return await Page(query.OrderBy(v => v.Name).ThenBy(v => v.Id), page, ct);
    }

    public async Task<VariationData> VariationAsync(Guid productId, Guid variationId, CancellationToken ct)
    {
        await RequireProduct(productId, ct);
        var variation = await VariationRows(productId).SingleOrDefaultAsync(v => v.Id == variationId, ct)
            ?? throw EntityNotFoundException.For<Variation>(variationId);
        var options = await Options([variationId], ct);
        return variation with { Options = options[variationId].ToArray() };
    }
}