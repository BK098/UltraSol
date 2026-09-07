//using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
//using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
//using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
//using UltraSol.Modules.Catalog.Domain.Catalog.Collections.ValueObjects;
//using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
//using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
//using UltraSol.Modules.Catalog.Domain.Catalog.Products;
//using UltraSol.Shared.Domain.Common.Entities;
//using UltraSol.Shared.Domain.Common.Exceptions;
//using Xunit;
//using CatalogCollection = UltraSol.Modules.Catalog.Domain.Catalog.Collections.Collection;

//namespace UltraSol.Modules.Catalog.Domain.Tests;

//public class TaxonomyTests
//{
//    private sealed class Reader : ICategoryHierarchyReader
//    {
//        internal readonly Dictionary<Guid, Category> Categories = [];
//        internal void Add(Category category) => Categories.Add(category.Id, category);
//        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
//        {
//            cancellationToken.ThrowIfCancellationRequested();
//            return Task.FromResult(Categories.GetValueOrDefault(id));
//        }
//        public Task<bool> HasNonArchivedChildrenAsync(Guid id, CancellationToken cancellationToken = default)
//        {
//            cancellationToken.ThrowIfCancellationRequested();
//            return Task.FromResult(Categories.Values.Any(x => x.ParentCategoryId == id && !x.IsArchived));
//        }
//    }

//    private static Product Published()
//    {
//        var product = Product.Create("Product");
//        ProductItem.Create(product, SKU.Create("SKU"), []);
//        product.Publish();
//        return product;
//    }

//    // Simulate corrupt persisted hierarchy without exposing mutation APIs to application callers.
//    private static void PersistedParent(Category category, Guid parentId) =>
//        typeof(Category).GetProperty(nameof(Category.ParentCategoryId))!.SetValue(category, parentId);

//    [Theory]
//    [InlineData("")]
//    [InlineData("  ")]
//    [InlineData(null)]
//    public void NamesAreRequired(string? name)
//    {
//        Assert.Throws<DomainException>(() => Brand.Create(name!));
//        Assert.Throws<DomainException>(() => Category.CreateRoot(name!));
//        Assert.Throws<DomainException>(() => CatalogCollection.CreateManual(name!));
//        var brand = Brand.Create("Original");
//        Assert.Throws<DomainException>(() => brand.Rename(name!));
//        Assert.Equal("Original", brand.Name);
//    }

//    [Fact]
//    public async Task MetadataAndPermanentArchiveAreConsistent()
//    {
//        var reader = new Reader();
//        var service = new CategoryHierarchyService(reader);
//        var brand = Brand.Create(" Brand ", " Description ");
//        var category = Category.CreateRoot(" Category ");
//        var collection = CatalogCollection.CreateManual(" Collection ");
//        Assert.Equal("Brand", brand.Name);
//        Assert.Equal("Description", brand.Description);
//        brand.Rename(" Renamed ");
//        category.Rename(" Renamed ");
//        collection.Rename(" Renamed ");
//        brand.ChangeDescription(" D ");
//        category.ChangeDescription(" D ");
//        collection.ChangeDescription(" D ");
//        Assert.Equal("D", category.Description);
//        Assert.Equal("D", collection.Description);
//        brand.Archive();
//        brand.Archive();
//        await service.ArchiveAsync(category);
//        await service.ArchiveAsync(category);
//        collection.Archive();
//        collection.Archive();
//        Assert.True(brand.IsArchived && category.IsArchived && collection.IsArchived);
//        Assert.Throws<DomainException>(() => brand.Rename("No"));
//        Assert.Throws<DomainException>(() => brand.ChangeDescription(null));
//        Assert.Throws<DomainException>(() => category.Rename("No"));
//        Assert.Throws<DomainException>(() => category.ChangeDescription(null));
//        Assert.Throws<DomainException>(() => collection.Rename("No"));
//        Assert.Throws<DomainException>(() => collection.ChangeDescription(null));
//        foreach (AggregateRoot aggregate in new AggregateRoot[] { brand, category, collection })
//        {
//            Assert.Throws<DomainException>(() => aggregate.MarkDeleted(null, DateTimeOffset.UtcNow));
//            Assert.Throws<DomainException>(() => aggregate.Restore());
//            Assert.False(aggregate.IsDeleted);
//        }
//    }

//    [Fact]
//    public async Task HierarchySupportsMovesAndRequiresChildrenHandledBeforeArchive()
//    {
//        var reader = new Reader();
//        var service = new CategoryHierarchyService(reader);
//        var root = Category.CreateRoot("Root");
//        reader.Add(root);
//        var child = await service.CreateChildAsync("Child", root.Id);
//        reader.Add(child);
//        var grandchild = await service.CreateChildAsync("Grandchild", child.Id);
//        reader.Add(grandchild);
//        Assert.Equal(root.Id, child.ParentCategoryId);
//        await Assert.ThrowsAsync<DomainException>(() => service.MoveAsync(root, grandchild.Id));
//        Assert.Null(root.ParentCategoryId);
//        await Assert.ThrowsAsync<DomainException>(() => service.MoveAsync(child, child.Id));
//        Assert.Equal(root.Id, child.ParentCategoryId);
//        await Assert.ThrowsAsync<DomainException>(() => service.ArchiveAsync(root));
//        Assert.False(root.IsArchived);
//        await service.MoveAsync(child, null);
//        await service.ArchiveAsync(root);
//        await Assert.ThrowsAsync<DomainException>(() => service.MoveAsync(child, root.Id));
//        await Assert.ThrowsAsync<DomainException>(() => service.MoveAsync(root, null));
//        await service.ArchiveAsync(grandchild);
//        await service.ArchiveAsync(child);
//        Assert.True(child.IsArchived);
//    }

//    [Fact]
//    public async Task HierarchyRejectsMissingCorruptOrCancelledChainsWithoutMutation()
//    {
//        var reader = new Reader();
//        var service = new CategoryHierarchyService(reader);
//        var target = Category.CreateRoot("Target");
//        var a = Category.CreateRoot("A");
//        var b = Category.CreateRoot("B");
//        reader.Add(a);
//        reader.Add(b);
//        await Assert.ThrowsAsync<DomainException>(() => service.MoveAsync(target, Guid.Empty));
//        await Assert.ThrowsAsync<DomainException>(() => service.MoveAsync(target, Guid.NewGuid()));
//        PersistedParent(a, Guid.NewGuid());
//        await Assert.ThrowsAsync<DomainException>(() => service.MoveAsync(target, a.Id));
//        PersistedParent(a, b.Id);
//        PersistedParent(b, a.Id);
//        await Assert.ThrowsAsync<DomainException>(() => service.MoveAsync(target, a.Id));
//        Assert.Null(target.ParentCategoryId);
//        using var cancellation = new CancellationTokenSource();
//        cancellation.Cancel();
//        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.MoveAsync(target, null, cancellation.Token));
//        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ArchiveAsync(target, cancellation.Token));
//        Assert.False(target.IsArchived);
//        Assert.Null(target.ParentCategoryId);
//    }

//    [Fact]
//    public async Task ProductReferencesAreOptionalIdOnlyAndRespectArchives()
//    {
//        var product = Published();
//        var brand = Brand.Create("Brand");
//        var otherBrand = Brand.Create("Other");
//        var category = Category.CreateRoot("Category");
//        var second = Category.CreateRoot("Second");
//        Assert.Null(product.BrandId);
//        Assert.Empty(product.CategoryIds);
//        product.AssignBrand(brand);
//        product.AssignBrand(brand);
//        product.AddCategory(category);
//        product.AddCategory(category);
//        product.AddCategory(second);
//        Assert.Equal(2, product.CategoryIds.Count);
//        Assert.Throws<NotSupportedException>(() => ((IList<Guid>)product.CategoryIds).Clear());
//        brand.Archive();
//        await new CategoryHierarchyService(new Reader()).ArchiveAsync(category);
//        Assert.Equal(brand.Id, product.BrandId);
//        Assert.Contains(category.Id, product.CategoryIds);
//        Assert.Equal(ProductStatus.Published, product.Status);
//        Assert.Throws<DomainException>(() => product.AssignBrand(brand));
//        Assert.Throws<DomainException>(() => product.AddCategory(category));
//        product.AssignBrand(otherBrand);
//        Assert.Equal(otherBrand.Id, product.BrandId);
//        product.ClearBrand();
//        product.ClearBrand();
//        product.RemoveCategory(category.Id);
//        product.RemoveCategory(category.Id);
//        Assert.Throws<DomainException>(() => product.RemoveCategory(Guid.Empty));
//        product.Archive();
//        Assert.Throws<DomainException>(() => product.AssignBrand(otherBrand));
//        Assert.Throws<DomainException>(() => product.ClearBrand());
//        Assert.Throws<DomainException>(() => product.AddCategory(second));
//        Assert.Throws<DomainException>(() => product.RemoveCategory(second.Id));
//    }

//    [Fact]
//    public void ManualCollectionReordersAtomicallyAndFiltersProductLifecycle()
//    {
//        var collection = CatalogCollection.CreateManual("Manual");
//        var a = Product.Create("Draft");
//        var b = Published();
//        collection.AddProduct(a);
//        collection.AddProduct(a);
//        collection.AddProduct(b);
//        Assert.Equal(2, collection.Entries.Count);
//        Assert.False(collection.Matches(a));
//        Assert.True(collection.Matches(b));
//        collection.ReorderProducts([b.Id, a.Id]);
//        var before = collection.Entries.ToArray();
//        foreach (var ids in new Guid[][] { [a.Id, a.Id], [a.Id], [a.Id, Guid.NewGuid()], [a.Id, Guid.Empty] })
//        {
//            Assert.Throws<DomainException>(() => collection.ReorderProducts(ids));
//            Assert.Equal(before, collection.Entries);
//        }
//        Assert.Equal(new[] { 0, 1 }, collection.Entries.Select(x => x.SortOrder));
//        Assert.Throws<NotSupportedException>(() => ((IList<CollectionEntry>)collection.Entries).Clear());
//        collection.RemoveProduct(b.Id);
//        collection.RemoveProduct(b.Id);
//        Assert.Single(collection.Entries);
//        Assert.Equal(0, collection.Entries[0].SortOrder);
//        collection.AddProduct(b);
//        b.Unpublish();
//        Assert.False(collection.Matches(b));
//        b.Publish();
//        Assert.True(collection.Matches(b));
//        b.Archive();
//        Assert.False(collection.Matches(b));
//        Assert.Throws<DomainException>(() => collection.AddProduct(b));
//        Assert.Throws<DomainException>(() => collection.ReplaceRules([Brand.Create("Brand")], []));
//        collection.Archive();
//        Assert.False(collection.Matches(a));
//        Assert.Throws<DomainException>(() => collection.AddProduct(a));
//        Assert.Throws<DomainException>(() => collection.RemoveProduct(a.Id));
//        Assert.Throws<DomainException>(() => collection.ReorderProducts([a.Id, b.Id]));
//    }

//    [Theory]
//    [InlineData(RuleMatchMode.All, false, false, false)]
//    [InlineData(RuleMatchMode.All, true, false, false)]
//    [InlineData(RuleMatchMode.All, false, true, false)]
//    [InlineData(RuleMatchMode.All, true, true, true)]
//    [InlineData(RuleMatchMode.Any, false, false, false)]
//    [InlineData(RuleMatchMode.Any, true, false, true)]
//    [InlineData(RuleMatchMode.Any, false, true, true)]
//    [InlineData(RuleMatchMode.Any, true, true, true)]
//    public void AutomaticRulesCombineGroups(RuleMatchMode mode, bool brandMatch, bool categoryMatch, bool expected)
//    {
//        var brand = Brand.Create("Brand");
//        var category = Category.CreateRoot("Category");
//        var collection = CatalogCollection.CreateAutomatic("Auto", [Brand.Create("Other"), brand],
//            [Category.CreateRoot("Other"), category], mode);
//        var product = Published();
//        if (brandMatch) product.AssignBrand(brand);
//        if (categoryMatch) product.AddCategory(category);
//        Assert.Equal(expected, collection.Matches(product));
//        Assert.Empty(collection.Entries);
//    }

//    [Theory]
//    [InlineData(RuleMatchMode.All)]
//    [InlineData(RuleMatchMode.Any)]
//    public void SingleRuleGroupWorksAndRulesAreImmutable(RuleMatchMode mode)
//    {
//        var a = Brand.Create("A");
//        var b = Brand.Create("B");
//        var category = Category.CreateRoot("Category");
//        var brands = new List<Brand> { b, a, a };
//        var rules = CollectionRuleSet.Create(brands, [category], mode);
//        var equivalent = CollectionRuleSet.Create([a, b], [category, category], mode);
//        brands.Clear();
//        Assert.Equal(rules, equivalent);
//        Assert.Equal(rules.GetHashCode(), equivalent.GetHashCode());
//        Assert.Equal(2, rules.BrandIds.Count);
//        Assert.Throws<NotSupportedException>(() => ((IList<Guid>)rules.BrandIds).Clear());
//        Assert.NotEqual(CollectionRuleSet.Create([a], [], mode), CollectionRuleSet.Create([], [category], mode));
//        var product = Published();
//        product.AssignBrand(a);
//        product.AddCategory(category);
//        Assert.True(CatalogCollection.CreateAutomatic("Brand", [a], [], mode).Matches(product));
//        Assert.True(CatalogCollection.CreateAutomatic("Category", [], [category], mode).Matches(product));
//        Assert.False(CatalogCollection.CreateAutomatic("Other", [b], [], mode).Matches(product));
//    }

//    [Fact]
//    public async Task AutomaticRulesUseDirectIdsAndRetainArchivedReferences()
//    {
//        var reader = new Reader();
//        var service = new CategoryHierarchyService(reader);
//        var parent = Category.CreateRoot("Parent");
//        reader.Add(parent);
//        var child = await service.CreateChildAsync("Child", parent.Id);
//        reader.Add(child);
//        var brand = Brand.Create("Brand");
//        var product = Published();
//        product.AssignBrand(brand);
//        product.AddCategory(child);
//        var collection = CatalogCollection.CreateAutomatic("Auto", [brand], [parent]);
//        Assert.False(collection.Matches(product));
//        collection.ReplaceRules([brand], [child]);
//        Assert.True(collection.Matches(product));
//        brand.Archive();
//        await service.ArchiveAsync(child);
//        Assert.True(collection.Matches(product));
//        var oldRules = collection.Rules;
//        Assert.Throws<DomainException>(() => collection.ReplaceRules([brand], [child]));
//        Assert.Same(oldRules, collection.Rules);
//        Assert.Throws<DomainException>(() => CatalogCollection.CreateAutomatic("Bad", [], [child]));
//        product.Unpublish();
//        Assert.False(collection.Matches(product));
//        product.Publish();
//        collection.Archive();
//        Assert.False(collection.Matches(product));
//        Assert.Throws<DomainException>(() => collection.ReplaceRules([Brand.Create("Fresh")], []));
//    }

//    [Fact]
//    public void RulesAndEntryValidationAndTypeBoundaries()
//    {
//        var brand = Brand.Create("Brand");
//        var auto = CatalogCollection.CreateAutomatic("Auto", [brand], []);
//        var product = Published();
//        Assert.Throws<DomainException>(() => auto.AddProduct(product));
//        Assert.Throws<DomainException>(() => auto.RemoveProduct(product.Id));
//        Assert.Throws<DomainException>(() => auto.ReorderProducts([]));
//        Assert.Throws<DomainException>(() => CollectionRuleSet.Create([], []));
//        Assert.Throws<DomainException>(() => CollectionRuleSet.Create([brand], [], (RuleMatchMode)0));
//        Assert.Throws<DomainException>(() => new CollectionEntry(Guid.Empty, 0));
//        Assert.Throws<DomainException>(() => new CollectionEntry(product.Id, -1));
//        Assert.Equal(new CollectionEntry(product.Id, 0), new CollectionEntry(product.Id, 0));
//        Assert.NotEqual(new CollectionEntry(product.Id, 0), new CollectionEntry(product.Id, 1));
//        var old = auto.Rules;
//        Assert.Throws<DomainException>(() => auto.ReplaceRules([], []));
//        Assert.Same(old, auto.Rules);
//        brand.Archive();
//        Assert.Throws<DomainException>(() => CatalogCollection.CreateAutomatic("Bad", [brand], []));
//    }
//}