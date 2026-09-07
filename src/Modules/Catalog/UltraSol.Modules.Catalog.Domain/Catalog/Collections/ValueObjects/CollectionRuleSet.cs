using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Guards;
using UltraSol.Shared.Domain.Common.ValueObjects;

namespace UltraSol.Modules.Catalog.Domain.Catalog.Collections.ValueObjects;

/// <summary>Immutable ID-based rules. Archive of referenced metadata does not rewrite existing rules.</summary>
public sealed class CollectionRuleSet : ValueObject
{
    public RuleMatchMode MatchMode { get; }
    internal static CollectionRuleSet FromStorage(RuleMatchMode mode, Guid[] brands, Guid[] categories) =>
        new(mode, brands, categories);
    public IReadOnlyList<Guid> BrandIds { get; }
    public IReadOnlyList<Guid> CategoryIds { get; }

    private CollectionRuleSet(RuleMatchMode matchMode, Guid[] brandIds, Guid[] categoryIds)
    {
        MatchMode = matchMode;
        BrandIds = Array.AsReadOnly(brandIds);
        CategoryIds = Array.AsReadOnly(categoryIds);
    }

    public static CollectionRuleSet Create(IEnumerable<Brand> brands, IEnumerable<Category> categories,
        RuleMatchMode matchMode = RuleMatchMode.All)
    {
        ArgumentNullException.ThrowIfNull(brands);
        ArgumentNullException.ThrowIfNull(categories);
        if (!Enum.IsDefined(matchMode))
        {
            throw new DomainException("Invalid rule match mode.");
        }
        var brandIds = brands.Select(brand =>
        {
            ArgumentNullException.ThrowIfNull(brand);
            brand.EnsureNotArchived();
            return Guard.Id(brand.Id);
        }).Distinct().Order().ToArray();
        var categoryIds = categories.Select(category =>
        {
            ArgumentNullException.ThrowIfNull(category);
            category.EnsureNotArchived();
            return Guard.Id(category.Id);
        }).Distinct().Order().ToArray();
        if (brandIds.Length == 0 && categoryIds.Length == 0)
        {
            throw new DomainException("Automatic collection needs at least one rule group.");
        }
        return new CollectionRuleSet(matchMode, brandIds, categoryIds);
    }

    internal bool Matches(Product product)
    {
        var brandMatches = product.BrandId.HasValue && BrandIds.Contains(product.BrandId.Value);
        var categoryMatches = product.CategoryIds.Any(CategoryIds.Contains);
        return MatchMode == RuleMatchMode.All
            ? (BrandIds.Count == 0 || brandMatches) && (CategoryIds.Count == 0 || categoryMatches)
            : (BrandIds.Count > 0 && brandMatches) || (CategoryIds.Count > 0 && categoryMatches);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MatchMode;
        yield return BrandIds.Count;
        foreach (var id in BrandIds)
        {
            yield return id;
        }
        yield return CategoryIds.Count;
        foreach (var id in CategoryIds)
        {
            yield return id;
        }
    }
}
