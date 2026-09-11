using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Infrastructure.Persistence.Seeding;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using Xunit;

namespace UltraSol.Modules.Catalog.Domain.Tests;

public class CatalogSeedTests
{
    private static Task<CatalogSeedData> ReadAsync() =>
        CatalogSeedData.ReadAsync(Path.Combine(AppContext.BaseDirectory, "seeds", "catalog.json"));

    [Fact]
    public void SeedSkuLookupTranslatesToPostgres()
    {
        using var context = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        var skus = new[] { SKU.Create("SEED-P001-01"), SKU.Create("SEED-P002-01") };
        var sql = context.ProductItems.Where(x => skus.Contains(x.Sku)).ToQueryString();
        Assert.Contains("sku", sql);
        Assert.Contains("ANY", sql);
    }

    [Fact]
    public async Task JsonBuildsCompleteCatalogThroughDomainRules()
    {
        var data = await ReadAsync();
        var roots = await data.BuildAsync();
        var products = roots.OfType<Product>().ToArray();
        var items = roots.OfType<ProductItem>().ToArray();
        var brands = roots.OfType<Brand>().ToArray();
        var categories = roots.OfType<Category>().ToArray();
        var collections = roots.OfType<Collection>().ToArray();
        Assert.Equal(100, products.Length);
        Assert.Equal(10, brands.Length);
        Assert.Equal(13, categories.Length);
        Assert.Equal(4, collections.Length);
        Assert.Equal(381, items.Length);
        Assert.Equal(items.Length, items.Select(x => x.Sku.Value).Distinct().Count());
        Assert.Equal(products.Length, products.Select(x => x.Name).Distinct().Count());
        Assert.All(products, product =>
        {
            var ownedItems = items.Where(x => x.ProductId == product.Id).ToArray();
            Assert.InRange(ownedItems.Length, 1, 10);
            Assert.Equal(ownedItems.Length, ownedItems.Select(x => x.OptionSignature.Value).Distinct().Count());
            Assert.Contains(brands, x => x.Id == product.BrandId);
            Assert.NotEmpty(product.CategoryIds);
            Assert.All(product.CategoryIds, id => Assert.Contains(categories, x => x.Id == id));
            Assert.Equal(ProductStatus.Published, product.Status);
            Assert.Empty(product.Media);
            Assert.All(ownedItems, item => product.ValidateSelections(item.OptionSelections));
        });
        Assert.Contains(products, x => items.Count(i => i.ProductId == x.Id) == 10);
        Assert.Contains(items, x => x.OptionSelections.Count == 0 && !x.IsBundle);
        Assert.Contains(products, x => x.Variations.Count == 2);
        Assert.All(items, x => Assert.Equal(ProductItemStatus.Active, x.Status));
        Assert.All(data.Products, x => Assert.Equal("", x.Media));
        Assert.All(data.Products.SelectMany(x => x.Items), x => Assert.Equal("", x.Media));
        Assert.All(categories.Where(x => x.ParentCategoryId.HasValue), child =>
            Assert.Contains(categories, parent => parent.Id == child.ParentCategoryId));
        Assert.Contains(categories, child => categories.Any(parent => parent.Id == child.ParentCategoryId && parent.ParentCategoryId.HasValue));
        var bundles = items.Where(x => x.IsBundle).ToArray();
        Assert.Equal(5, bundles.Length);
        Assert.All(bundles, bundle =>
        {
            Assert.NotEmpty(bundle.BundleDefinition!.Components);
            Assert.All(bundle.BundleDefinition.Components, component =>
            {
                Assert.True(component.Quantity > 0);
                Assert.Contains(items, x => x.Id == component.ProductItemId && !x.IsBundle);
            });
        });
        Assert.Equal(2, collections.Count(x => x.Type == CollectionType.Manual));
        Assert.Equal(2, collections.Count(x => x.Type == CollectionType.Automatic));
        Assert.All(collections, collection => Assert.Contains(products, collection.Matches));
    }

    [Fact]
    public async Task RejectsDuplicateSelectionsAndInvalidBundleBeforePersistence()
    {
        var data = await ReadAsync();
        var product = data.Products.First(x => x.Items.Length == 2);
        product.Items[1] = product.Items[1] with { Selections = product.Items[0].Selections };
        await Assert.ThrowsAsync<InvalidDataException>(() => data.BuildAsync());
        data = await ReadAsync();
        var bundle = data.Products.SelectMany(x => x.Items).First(x => x.Components.Length > 0);
        bundle.Components[0] = bundle.Components[0] with { Quantity = 0 };
        await Assert.ThrowsAnyAsync<Exception>(() => data.BuildAsync());
    }

    [Fact]
    public async Task RejectsCyclesMissingReferencesAndTooManyItems()
    {
        var data = await ReadAsync();
        data.Categories[0] = data.Categories[0] with { Parent = "category-1" };
        await Assert.ThrowsAnyAsync<Exception>(() => data.BuildAsync());
        data = await ReadAsync();
        data.Products[0] = data.Products[0] with { Brand = "missing" };
        await Assert.ThrowsAsync<KeyNotFoundException>(() => data.BuildAsync());
        data = await ReadAsync();
        data.Products[0] = data.Products[0] with { Items = Enumerable.Repeat(data.Products[0].Items[0], 11).ToArray() };
        await Assert.ThrowsAsync<InvalidDataException>(() => data.BuildAsync());
    }

    [Fact]
    public async Task PopulatedMediaIsImported()
    {
        var data = await ReadAsync();
        data.Brands[0] = data.Brands[0] with { Media = "https://example.com/brand.png" };
        data.Products[0] = data.Products[0] with { Media = "https://example.com/product.png" };
        data.Products[0].Items[0] = data.Products[0].Items[0] with { Media = "https://example.com/item.png" };
        var roots = await data.BuildAsync();
        Assert.Contains(roots.OfType<Brand>(), x => x.LogoUrl == data.Brands[0].Media);
        Assert.Contains(roots.OfType<Product>(), x => x.Media.Any(m => m.Url == data.Products[0].Media));
        Assert.Contains(roots.OfType<ProductItem>(), x => x.Media.Any(m => m.Url == data.Products[0].Items[0].Media));
    }
}