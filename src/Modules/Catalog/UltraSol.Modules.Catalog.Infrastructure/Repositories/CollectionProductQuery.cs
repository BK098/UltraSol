using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Guards;

namespace UltraSol.Modules.Catalog.Infrastructure.Repositories;

public sealed class CollectionProductQuery(CatalogDbContext db) : ICollectionProductQuery
{
    public async Task<IReadOnlyList<Guid>> GetProductIdsAsync(Guid collectionId, Guid? afterId = null,
        int pageSize = 50, CancellationToken cancellationToken = default)
    {
        Guard.Id(collectionId);
        if (afterId.HasValue)
        {
            Guard.Id(afterId.Value);
        }
        if (pageSize is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }
        var query =
            from p in db.Products.AsNoTracking()
            from c in db.Collections.AsNoTracking()
            where c.Id == collectionId && c.Status == CollectionStatus.Published && p.Status == ProductStatus.Published
            let hasBrands = db.Set<RuleBrandRow>().Any(r => r.CollectionId == c.Id)
            let hasCategories = db.Set<RuleCategoryRow>().Any(r => r.CollectionId == c.Id)
            let matchesBrand = db.Set<RuleBrandRow>().Any(r => r.CollectionId == c.Id && r.BrandId == p.BrandId)
            let matchesCategory = db.Set<RuleCategoryRow>().Any(r => r.CollectionId == c.Id &&
                db.Set<ProductCategoryRow>().Any(pc => pc.ProductId == p.Id && pc.CategoryId == r.CategoryId))
            where c.Type == CollectionType.Manual
                ? db.Set<CollectionEntryRow>().Any(e => e.CollectionId == c.Id && e.ProductId == p.Id)
                : EF.Property<RuleMatchMode?>(c, "StoredMatchMode") == RuleMatchMode.All
                    ? (!hasBrands || matchesBrand) && (!hasCategories || matchesCategory)
                    : matchesBrand || matchesCategory
            select p.Id;
        if (afterId.HasValue)
        {
            query = query.Where(id => id.CompareTo(afterId.Value) > 0);
        }
        return await query.OrderBy(id => id).Take(pageSize).ToListAsync(cancellationToken);
    }
}