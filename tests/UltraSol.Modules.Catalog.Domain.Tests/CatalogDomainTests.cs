using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Catalog.Domain.Tests;

public class CatalogDomainTests
{
    private static ProductItem DefaultItem(Product? product = null, string sku = "ITEM") =>
        ProductItem.Create(product ?? Product.Create("Product"), SKU.Create(sku), []);

    private static (Product Product, Guid Variation, Guid Option) Configured()
    {
        var product = Product.Create("Shirt");
        var variation = product.AddVariation("Color");
        return (product, variation, product.AddVariationOption(variation, "Black"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NamesAndSkuAreRequired(string? value)
    {
        Assert.Throws<DomainException>(() => Product.Create(value!));
        Assert.Throws<DomainException>(() => SKU.Create(value!));
        var product = Product.Create("Valid");
        Assert.Throws<DomainException>(() => product.AddVariation(value!));
        var variation = product.AddVariation("Color");
        Assert.Throws<DomainException>(() => product.AddVariationOption(variation, value!));
    }

    [Fact]
    public void ProductMetadataAndDraftStructureCanBeEdited()
    {
        var (product, variation, option) = Configured();
        Assert.NotEqual(Guid.Empty, product.Id);
        product.Rename(" New shirt ");
        product.ChangeDescription(" Description ");
        product.RenameVariation(variation, "Colour");
        product.RenameVariationOption(variation, option, "Dark");
        Assert.Equal("New shirt", product.Name);
        Assert.Equal("Description", product.Description);
        Assert.Equal("Colour", product.Variations[0].Name);
        Assert.Equal("Dark", product.Variations[0].Options[0].Value);
        product.RemoveVariationOption(variation, option);
        product.RemoveVariation(variation);
        Assert.Empty(product.Variations);
    }

    [Fact]
    public void DuplicateNamesAreRejectedWithoutChangingState()
    {
        var (product, variation, _) = Configured();
        Assert.Throws<DomainException>(() => product.AddVariation(" color "));
        Assert.Throws<DomainException>(() => product.AddVariationOption(variation, " BLACK "));
        var size = product.AddVariation("Size");
        Assert.Throws<DomainException>(() => product.RenameVariation(size, "Color"));
        var white = product.AddVariationOption(variation, "White");
        Assert.Throws<DomainException>(() => product.RenameVariationOption(variation, white, "Black"));
        Assert.Equal("White", product.Variations[0].Options[1].Value);
        Assert.Equal("Size", product.Variations[1].Name);
    }

    [Fact]
    public void LifecycleRequiresItemAndArchiveIsTerminal()
    {
        var product = Product.Create("Product");
        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Throws<DomainException>(() => product.Publish());
        Assert.Throws<DomainException>(() => product.Unpublish());
        DefaultItem(product);
        product.Publish();
        Assert.Throws<DomainException>(() => product.Publish());
        product.Unpublish();
        product.Publish();
        product.Archive();
        product.Archive();
        Assert.Equal(ProductStatus.Archived, product.Status);
        Assert.Throws<DomainException>(() => product.Publish());
        Assert.Throws<DomainException>(() => product.Unpublish());
        Assert.Throws<DomainException>(() => product.Rename("Other"));
        Assert.Throws<DomainException>(() => product.ChangeDescription("Other"));
        Assert.Throws<DomainException>(() => DefaultItem(product));
        Assert.Throws<DomainException>(() => product.AddMedia("https://example.com/a"));
    }

    [Fact]
    public void PublishRaisesProductPublishedEvent()
    {
        var product = Product.Create("Product");
        DefaultItem(product);

        product.Publish();

        Assert.Contains(product.DomainEvents, domainEvent => domainEvent is Product.ProductPublished published && published.ProductId == product.Id);
    }

    [Fact]
    public void FirstItemLocksStructureButAllowsNewOptionsAndMetadata()
    {
        var (product, variation, option) = Configured();
        ProductItem.Create(product, SKU.Create("BLACK"), [new(variation, option)]);
        Assert.True(product.IsStructureLocked);
        Assert.Throws<DomainException>(() => product.AddVariation("Size"));
        Assert.Throws<DomainException>(() => product.RenameVariation(variation, "Colour"));
        Assert.Throws<DomainException>(() => product.RemoveVariation(variation));
        Assert.Throws<DomainException>(() => product.RenameVariationOption(variation, option, "Dark"));
        Assert.Throws<DomainException>(() => product.RemoveVariationOption(variation, option));
        product.Publish();
        var white = product.AddVariationOption(variation, "White");
        ProductItem.Create(product, SKU.Create("WHITE"), [new(variation, white)]);
        product.Rename("New title");
        product.AddMedia("https://example.com/a");
        product.Archive();
        Assert.Throws<DomainException>(() => product.AddVariationOption(variation, "Red"));
    }

    [Fact]
    public void InvalidSelectionsNeverLockProduct()
    {
        var (product, variation, option) = Configured();
        var other = Configured();
        var invalid = new OptionSelection[][]
        {
            [],
            [new(variation, option), new(variation, option)],
            [new(other.Variation, other.Option)],
            [new(variation, other.Option)],
            [null!]
        };
        foreach (var selections in invalid)
        {
            Assert.Throws<DomainException>(() => ProductItem.Create(product, SKU.Create("SKU"), selections));
            Assert.False(product.IsStructureLocked);
        }
        Assert.Throws<ArgumentNullException>(() => ProductItem.Create(product, null!, [new(variation, option)]));
        Assert.False(product.IsStructureLocked);
        var size = product.AddVariation("Size");
        var medium = product.AddVariationOption(size, "M");
        Assert.Throws<DomainException>(() => ProductItem.Create(product, SKU.Create("SKU"), [new(variation, medium), new(size, option)]));
        Assert.False(product.IsStructureLocked);
    }

    [Fact]
    public void PlainProductOnlyAcceptsEmptySelections()
    {
        var product = Product.Create("Plain");
        Assert.Throws<DomainException>(() => ProductItem.Create(product, SKU.Create("SKU"), [new(Guid.NewGuid(), Guid.NewGuid())]));
        var item = DefaultItem(product);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Empty(item.OptionSelections);
        Assert.Equal("", item.OptionSignature.Value);
    }

    [Fact]
    public void ValueObjectsValidateAndCompareByValue()
    {
        Assert.Equal(SKU.Create(" abc "), SKU.Create("ABC"));
        Assert.Equal(SKU.Create("abc").GetHashCode(), SKU.Create("ABC").GetHashCode());
        Assert.NotEqual(SKU.Create("ABC"), SKU.Create("DEF"));
        Assert.Equal(64, SKU.Create(new string('a', 64)).Value.Length);
        Assert.Throws<DomainException>(() => SKU.Create(new string('a', 65)));
        Assert.Throws<DomainException>(() => new OptionSelection(Guid.Empty, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => new OptionSelection(Guid.NewGuid(), Guid.Empty));
        Assert.Throws<DomainException>(() => new BundleComponent(Guid.Empty, 1));
        var id = Guid.NewGuid();
        Assert.Equal(new BundleComponent(id, 2), new BundleComponent(id, 2));
        Assert.NotEqual(new BundleComponent(id, 1), new BundleComponent(id, 2));
        Assert.Throws<DomainException>(() => new BundleComponent(id, 0));
    }

    [Fact]
    public void SignatureAndSelectionsAreImmutableAndOrderIndependent()
    {
        var (product, color, black) = Configured();
        var size = product.AddVariation("Size");
        var medium = product.AddVariationOption(size, "M");
        var selections = new List<OptionSelection> { new(color, black), new(size, medium) };
        var expected = OptionSignature.Create(selections.AsEnumerable().Reverse());
        var item = ProductItem.Create(product, SKU.Create("SKU"), selections);
        selections.Clear();
        Assert.Equal(expected, item.OptionSignature);
        Assert.Equal(2, item.OptionSelections.Count);
        Assert.Equal(new OptionSelection(color, black), item.OptionSelections[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<OptionSelection>)item.OptionSelections).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Variation>)product.Variations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<VariationOption>)product.Variations[0].Options).Clear());
    }

    [Fact]
    public void ProductMediaMaintainsPrimaryAndContiguousOrder()
    {
        var product = Product.Create("Product");
        var first = product.AddMedia("https://example.com/a", " A ");
        var second = product.AddMedia("http://example.com/b");
        Assert.True(product.Media[0].IsPrimary);
        product.SetPrimaryMedia(second);
        product.ReorderMedia([second, first]);
        Assert.Equal(second, product.Media[0].Id);
        Assert.Equal([0, 1], product.Media.Select(x => x.SortOrder));
        product.UpdateMedia(first, "https://example.com/new", " New ");
        Assert.Equal("New", product.Media[1].AltText);
        Assert.Throws<DomainException>(() => product.ReorderMedia([first, first]));
        Assert.Equal(second, product.Media[0].Id);
        product.RemoveMedia(second);
        Assert.True(product.Media[0].IsPrimary);
        Assert.Equal(0, product.Media[0].SortOrder);
        product.RemoveMedia(first);
        Assert.Empty(product.Media);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/relative")]
    [InlineData("file:///local")]
    [InlineData("ftp://example.com/image")]
    public void InvalidMediaIsRejectedAtomically(string url)
    {
        var product = Product.Create("Product");
        var item = DefaultItem(product);
        Assert.Throws<DomainException>(() => product.AddMedia(url));
        Assert.Throws<DomainException>(() => item.AddMedia(product, url));
        var id = item.AddMedia(product, "https://example.com/old", "old");
        Assert.Throws<DomainException>(() => item.UpdateMedia(product, id, url, "new"));
        Assert.Equal("old", item.Media[0].AltText);
        Assert.Equal("https://example.com/old", item.Media[0].Url);
    }

    [Fact]
    public void ItemMediaRequiresEditableOwnerForEveryMutation()
    {
        var product = Product.Create("Product");
        var item = DefaultItem(product);
        var other = Product.Create("Other");
        var first = item.AddMedia(product, "https://example.com/a");
        var second = item.AddMedia(product, "https://example.com/b");
        item.UpdateMedia(product, first, "https://example.com/new");
        item.SetPrimaryMedia(product, second);
        item.ReorderMedia(product, [second, first]);
        Assert.Equal(second, item.Media[0].Id);
        item.RemoveMedia(product, second);
        Assert.True(item.Media[0].IsPrimary);
        void Reject(Product owner)
        {
            Assert.Throws<DomainException>(() => item.AddMedia(owner, "https://example.com/a"));
            Assert.Throws<DomainException>(() => item.UpdateMedia(owner, first, "https://example.com/a"));
            Assert.Throws<DomainException>(() => item.SetPrimaryMedia(owner, first));
            Assert.Throws<DomainException>(() => item.ReorderMedia(owner, [first]));
            Assert.Throws<DomainException>(() => item.RemoveMedia(owner, first));
        }
        Reject(other);
        product.Archive();
        Reject(product);
        Assert.Single(item.Media);
    }

    [Fact]
    public void BundleCopiesAndCanonicalizesItsComposition()
    {
        var a = DefaultItem(sku: "A");
        var b = DefaultItem(sku: "B");
        var source = new List<(ProductItem, int)> { (a, 2), (b, 1) };
        var bundle = ProductItem.CreateBundle(Product.Create("Bundle"), SKU.Create("BUNDLE"), [], source);
        var equivalent = ProductItem.CreateBundle(Product.Create("Other"), SKU.Create("OTHER"), [], source.AsEnumerable().Reverse());
        source.Clear();
        Assert.True(bundle.IsBundle);
        Assert.Equal(bundle.BundleDefinition, equivalent.BundleDefinition);
        Assert.Equal(2, bundle.BundleDefinition!.Components.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<BundleComponent>)bundle.BundleDefinition.Components).Clear());
    }

    [Fact]
    public void InvalidBundleDoesNotLockProduct()
    {
        var product = Product.Create("Bundle");
        var component = DefaultItem();
        var nested = ProductItem.CreateBundle(Product.Create("Nested"), SKU.Create("NESTED"), [], [(component, 1)]);
        var invalid = new (ProductItem, int)[][]
        {
            [], [(component, 0)], [(component, -1)], [(component, 1), (component, 2)], [(nested, 1)], [(null!, 1)]
        };
        foreach (var components in invalid)
        {
            Assert.Throws<DomainException>(() => ProductItem.CreateBundle(product, SKU.Create("BUNDLE"), [], components));
            Assert.False(product.IsStructureLocked);
        }
    }

    [Fact]
    public void SoftDeleteAndRestoreAreBlockedThroughBaseType()
    {
        var product = Product.Create("Product");
        var item = DefaultItem(product);
        foreach (AggregateRoot aggregate in new AggregateRoot[] { product, item })
        {
            Assert.Throws<DomainException>(() => aggregate.MarkDeleted("actor", DateTimeOffset.UtcNow));
            Assert.Throws<DomainException>(() => aggregate.Restore());
            Assert.False(aggregate.IsDeleted);
        }
    }
}
