using System.Text.Json;
using System.Text.Json.Serialization;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Domain.Common.Entities;

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence.Seeding;

public sealed record CatalogSeedData(BrandSeed[] Brands, CategorySeed[] Categories, ProductSeed[] Products, CollectionSeed[] Collections)
{
    public static async Task<CatalogSeedData> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
        };
        return await JsonSerializer.DeserializeAsync<CatalogSeedData>(stream, options, cancellationToken)
            ?? throw new InvalidDataException("Catalog seed file cannot be null.");
    }

    public async Task<IReadOnlyList<AggregateRoot>> BuildAsync(CancellationToken cancellationToken = default)
    {
        var brands = Brands.ToDictionary(x => x.Key, x => Brand.Create(x.Name, x.Description));
        foreach (var source in Brands)
        {
            if (!string.IsNullOrWhiteSpace(source.Media))
            {
                brands[source.Key].UpdateLogo(source.Media);
            }
        }
        var categories = Categories.ToDictionary(x => x.Key, x => Category.CreateRoot(x.Name, x.Description));
        var hierarchy = new CategoryHierarchyService(new SeedHierarchyReader(categories.Values.ToArray()));
        foreach (var source in Categories)
        {
            if (source.Parent is not null)
            {
                await hierarchy.MoveAsync(categories[source.Key], categories[source.Parent].Id, cancellationToken);
            }
        }

        var products = Products.ToDictionary(x => x.Key, x => Product.Create(x.Name, x.Description));
        var items = new Dictionary<string, ProductItem>(StringComparer.OrdinalIgnoreCase);
        var signatures = new HashSet<(Guid ProductId, string Signature)>();
        foreach (var source in Products)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (source.Items.Length is < 1 or > 10)
            {
                throw new InvalidDataException($"Product '{source.Key}' must contain 1 to 10 items.");
            }
            var product = products[source.Key];
            product.AssignBrand(brands[source.Brand]);
            foreach (var category in source.Categories)
            {
                product.AddCategory(categories[category]);
            }
            if (!string.IsNullOrWhiteSpace(source.Media))
            {
                product.AddMedia(source.Media);
            }
            foreach (var variation in source.Variations)
            {
                var id = product.AddVariation(variation.Key);
                foreach (var option in variation.Value)
                {
                    product.AddVariationOption(id, option);
                }
            }
        }

        // Regular items are built first so bundle references do not depend on JSON order.
        var itemSources = Products.SelectMany(p => p.Items.Select(i => (Product: products[p.Key], Item: i))).ToArray();
        foreach (var (product, source) in itemSources.OrderBy(x => x.Item.Components.Length > 0))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selections = source.Selections.Select(selection =>
            {
                var variation = product.Variations.Single(x => x.Name == selection.Key);
                return new OptionSelection(variation.Id, variation.Options.Single(x => x.Value == selection.Value).Id);
            }).ToArray();
            var sku = SKU.Create(source.Sku);
            var item = source.Components.Length == 0
                ? ProductItem.Create(product, sku, selections)
                : ProductItem.CreateBundle(product, sku, selections,
                    source.Components.Select(x => (items[SKU.Create(x.Sku).Value], x.Quantity)));
            if (!signatures.Add((product.Id, item.OptionSignature.Value)))
            {
                throw new InvalidDataException($"Duplicate option combination on '{product.Name}'.");
            }
            if (!string.IsNullOrWhiteSpace(source.Media))
            {
                item.AddMedia(product, source.Media);
            }
            item.Activate();
            items.Add(sku.Value, item);
        }
        foreach (var product in products.Values)
        {
            product.Publish();
        }

        var collections = Collections.ToDictionary(x => x.Key, source =>
        {
            if (source.Type == CollectionType.Manual && (source.Brands.Length > 0 || source.Categories.Length > 0) ||
                source.Type == CollectionType.Automatic && source.Products.Length > 0)
            {
                throw new InvalidDataException($"Collection '{source.Key}' cannot mix manual membership and automatic rules.");
            }
            var collection = source.Type switch
            {
                CollectionType.Manual => Collection.CreateManual(source.Name, source.Description),
                CollectionType.Automatic => Collection.CreateAutomatic(source.Name, source.Brands.Select(x => brands[x]),
                    source.Categories.Select(x => categories[x]), source.MatchMode, source.Description),
                _ => throw new InvalidDataException($"Invalid collection type on '{source.Key}'.")
            };
            foreach (var key in source.Products)
            {
                collection.AddProduct(products[key]);
            }
            collection.Publish();
            return collection;
        });
        return [.. brands.Values, .. categories.Values, .. products.Values, .. items.Values, .. collections.Values];
    }

    private sealed class SeedHierarchyReader(Category[] categories) : ICategoryHierarchyReader
    {
        public Task<Category?> GetByIdAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(categories.SingleOrDefault(x => x.Id == categoryId));

        public Task<bool> HasNonArchivedChildrenAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(categories.Any(x => x.ParentCategoryId == categoryId && !x.IsArchived));
    }
}

public sealed record BrandSeed(string Key, string Name, string Description, string Media);
public sealed record CategorySeed(string Key, string Name, string Description, string? Parent);
public sealed record ProductSeed(string Key, string Name, string Description, string Brand, string[] Categories,
    string Media, Dictionary<string, string[]> Variations, ItemSeed[] Items);
public sealed record ItemSeed(string Sku, string Media, Dictionary<string, string> Selections, ComponentSeed[] Components);
public sealed record ComponentSeed(string Sku, int Quantity);
public sealed record CollectionSeed(string Key, string Name, string Description, CollectionType Type,
    string[] Products, string[] Brands, string[] Categories, RuleMatchMode MatchMode);