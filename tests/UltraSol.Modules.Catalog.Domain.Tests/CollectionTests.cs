using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using UltraSol.Modules.Catalog.Application.Features.Collections.Commands;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Catalog.Domain.Tests;

public class CollectionTests
{
    [Fact]
    public void LifecycleAndVisibility()
    {
        var collection = Collection.CreateManual(" Summer ");
        var product = Product.Create("Shirt");
        ProductItem.Create(product, SKU.Create("SHIRT"), []);
        product.Publish();
        collection.AddProduct(product);
        Assert.Equal(CollectionStatus.Draft, collection.Status);
        Assert.False(collection.Matches(product));
        Assert.Throws<DomainException>(() => collection.Unpublish());
        collection.Publish();
        Assert.True(collection.Matches(product));
        Assert.Throws<DomainException>(() => collection.Publish());
        collection.Unpublish();
        Assert.False(collection.Matches(product));
        collection.Publish();
        product.Unpublish();
        Assert.False(collection.Matches(product));
        collection.Archive();
        collection.Archive();
        Assert.Equal(CollectionStatus.Archived, collection.Status);
        Assert.Throws<DomainException>(() => collection.Publish());
        Assert.Throws<DomainException>(() => collection.Unpublish());
        Assert.Throws<DomainException>(() => collection.Rename("New"));
        Assert.Throws<DomainException>(() => collection.AddProduct(product));
        Assert.Throws<DomainException>(() => collection.RemoveProduct(product.Id));
        Assert.Throws<DomainException>(() => collection.ReorderProducts([product.Id]));
    }

    [Fact]
    public void ManualMembershipIsIdempotentAndOrderMustBeExact()
    {
        var collection = Collection.CreateManual("Manual");
        collection.ReorderProducts([]);
        collection.Publish();
        var first = Product.Create("First");
        var second = Product.Create("Second");
        collection.AddProduct(first);
        collection.AddProduct(first);
        collection.AddProduct(second);
        Assert.Equal(2, collection.Entries.Count);
        Assert.Throws<DomainException>(() => collection.ReorderProducts([first.Id]));
        Assert.Throws<DomainException>(() => collection.ReorderProducts([first.Id, first.Id]));
        Assert.Throws<DomainException>(() => collection.ReorderProducts([first.Id, Guid.NewGuid()]));
        collection.ReorderProducts([second.Id, first.Id]);
        Assert.Equal(second.Id, collection.Entries[0].ProductId);
        Assert.Equal(0, collection.Entries[0].SortOrder);
        collection.RemoveProduct(first.Id);
        collection.RemoveProduct(first.Id);
        Assert.Single(collection.Entries);
        second.Archive();
        Assert.Throws<DomainException>(() => collection.AddProduct(second));
    }

    [Fact]
    public void AutomaticRulesRequireLiveReferencesAndPublishedCollection()
    {
        var brand = Brand.Create("Brand");
        var category = Category.CreateRoot("Category");
        var collection = Collection.CreateAutomatic("Auto", [brand], [category]);
        var product = Product.Create("Product");
        product.AssignBrand(brand);
        product.AddCategory(category);
        ProductItem.Create(product, SKU.Create("AUTO"), []);
        product.Publish();
        Assert.False(collection.Matches(product));
        collection.Publish();
        Assert.True(collection.Matches(product));
        Assert.Throws<DomainException>(() => collection.AddProduct(product));
        Assert.Throws<DomainException>(() => collection.RemoveProduct(product.Id));
        Assert.Throws<DomainException>(() => collection.ReorderProducts([]));
        Assert.Throws<DomainException>(() => Collection.CreateAutomatic("Empty", [], []));
        brand.Archive();
        Assert.Throws<DomainException>(() => Collection.CreateAutomatic("Archived", [brand], []));
        category.Archive();
        Assert.Throws<DomainException>(() => Collection.CreateAutomatic("Archived", [], [category]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NamesAreRequired(string? name)
    {
        Assert.False(new CreateCollectionValidator().Validate(new CreateCollectionCommand(new(name, null, CollectionType.Manual))).IsValid);
        Assert.False(new RenameCollectionValidator().Validate(new RenameCollectionCommand(Guid.NewGuid(), new(name))).IsValid);
    }

    [Fact]
    public void CreateValidationCoversBodyEnumsAndRules()
    {
        var validator = new CreateCollectionValidator();
        Assert.False(validator.Validate(new CreateCollectionCommand(null)).IsValid);
        Assert.False(validator.Validate(new CreateCollectionCommand(new("A", null, (CollectionType)99))).IsValid);
        Assert.False(validator.Validate(new CreateCollectionCommand(new("A", null, CollectionType.Manual, MatchMode: (RuleMatchMode)99))).IsValid);
        Assert.False(validator.Validate(new CreateCollectionCommand(new("A", null, CollectionType.Manual, [Guid.NewGuid()]))).IsValid);
        Assert.False(validator.Validate(new CreateCollectionCommand(new("A", null, CollectionType.Automatic))).IsValid);
        Assert.False(validator.Validate(new CreateCollectionCommand(new("A", null, CollectionType.Automatic, [Guid.Empty]))).IsValid);
        Assert.False(validator.Validate(new CreateCollectionCommand(new("A", null, CollectionType.Automatic, CategoryIds: [Guid.Empty]))).IsValid);
        Assert.True(validator.Validate(new CreateCollectionCommand(new("A", null, CollectionType.Manual))).IsValid);
        Assert.True(validator.Validate(new CreateCollectionCommand(new("A", null, CollectionType.Automatic, [Guid.NewGuid()]))).IsValid);
    }

    [Fact]
    public void MutationValidationCoversIdsBodiesAndOrder()
    {
        Assert.False(new ArchiveCollectionValidator().Validate(new ArchiveCollectionCommand(Guid.Empty)).IsValid);
        Assert.False(new PublishCollectionValidator().Validate(new PublishCollectionCommand(Guid.Empty)).IsValid);
        Assert.False(new UnpublishCollectionValidator().Validate(new UnpublishCollectionCommand(Guid.Empty)).IsValid);
        Assert.False(new RenameCollectionValidator().Validate(new RenameCollectionCommand(Guid.Empty, null)).IsValid);
        Assert.False(new AddProductToCollectionValidator().Validate(new AddProductToCollectionCommand(Guid.Empty, Guid.Empty)).IsValid);
        Assert.False(new RemoveProductFromCollectionValidator().Validate(new RemoveProductFromCollectionCommand(Guid.Empty, Guid.Empty)).IsValid);
        var validator = new ReorderCollectionProductsValidator();
        var id = Guid.NewGuid();
        Assert.False(validator.Validate(new ReorderCollectionProductsCommand(Guid.Empty, new([]))).IsValid);
        Assert.False(validator.Validate(new ReorderCollectionProductsCommand(id, null)).IsValid);
        Assert.False(validator.Validate(new ReorderCollectionProductsCommand(id, new(null))).IsValid);
        Assert.False(validator.Validate(new ReorderCollectionProductsCommand(id, new([Guid.Empty]))).IsValid);
        Assert.False(validator.Validate(new ReorderCollectionProductsCommand(id, new([id, id]))).IsValid);
        Assert.True(validator.Validate(new ReorderCollectionProductsCommand(id, new([]))).IsValid);
    }

    [Fact]
    public async Task CommandsResolveThroughMediatorAndReturnCollectionIdWithinTransaction()
    {
        using var fixture = new HandlerFixture();
        var created = await fixture.Sender.Send(new CreateCollectionCommand(new("Manual", null, CollectionType.Manual)));
        Assert.Equal(201, created.StatusCode);
        var id = Assert.IsType<Guid>(created.Data);
        Assert.Equal(CollectionStatus.Draft, fixture.Collections[id].Status);
        var product = Product.Create("Product");
        fixture.Products.Add(product.Id, product);
        var commands = new IRequest<ApiResult<object>>[]
        {
            new RenameCollectionCommand(id, new("Renamed")),
            new AddProductToCollectionCommand(id, product.Id),
            new AddProductToCollectionCommand(id, product.Id),
            new ReorderCollectionProductsCommand(id, new([product.Id])),
            new RemoveProductFromCollectionCommand(id, product.Id),
            new RemoveProductFromCollectionCommand(id, product.Id),
            new PublishCollectionCommand(id),
            new UnpublishCollectionCommand(id),
            new ArchiveCollectionCommand(id)
        };
        foreach (var command in commands)
        {
            var result = await fixture.Sender.Send(command);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal(id, Assert.IsType<Guid>(result.Data));
        }
        Assert.Equal(commands.Length + 1, fixture.Commits);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Sender.Send(new PublishCollectionCommand(id)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Sender.Send(new PublishCollectionCommand(Guid.NewGuid())));
        Assert.Equal(commands.Length + 1, fixture.Commits);
    }

    [Fact]
    public async Task AutomaticCreateLoadsRulesAndRejectsMissingReferences()
    {
        using var fixture = new HandlerFixture();
        var brand = Brand.Create("Brand");
        var category = Category.CreateRoot("Category");
        fixture.Brands.Add(brand.Id, brand);
        fixture.Categories.Add(category.Id, category);
        var result = await fixture.Sender.Send(new CreateCollectionCommand(new("Auto", null, CollectionType.Automatic, [brand.Id], [category.Id])));
        var collection = fixture.Collections[Assert.IsType<Guid>(result.Data)];
        Assert.Equal(new[] { brand.Id }, collection.Rules!.BrandIds);
        Assert.Equal(new[] { category.Id }, collection.Rules.CategoryIds);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Sender.Send(
            new CreateCollectionCommand(new("Missing", null, CollectionType.Automatic, [Guid.NewGuid()]))));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Sender.Send(
            new AddProductToCollectionCommand(collection.Id, Guid.NewGuid())));
        Assert.Equal(1, fixture.Commits);
    }

    private sealed class HandlerFixture : IDisposable
    {
        public Dictionary<Guid, Collection> Collections { get; } = [];
        public Dictionary<Guid, Product> Products { get; } = [];
        public Dictionary<Guid, Brand> Brands { get; } = [];
        public Dictionary<Guid, Category> Categories { get; } = [];
        public int Commits { get; private set; }
        private bool _inTransaction;
        private readonly ServiceProvider _services;
        public ISender Sender => _services.GetRequiredService<ISender>();

        public HandlerFixture()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<CreateCollectionCommand>());
            services.AddSingleton(Repository<ICollectionRepository, Collection>(Collections, x => x.Id));
            services.AddSingleton(Repository<IProductRepository, Product>(Products, x => x.Id));
            services.AddSingleton(Repository<IBrandRepository, Brand>(Brands, x => x.Id));
            services.AddSingleton(Repository<ICategoryRepository, Category>(Categories, x => x.Id));
            services.AddSingleton(Proxy<ICatalogUnitOfWork>((method, args) =>
            {
                Assert.Equal("ExecuteInTransactionAsync", method.Name);
                return Execute((Func<CancellationToken, Task<ApiResult<object>>>)args[0]!, (CancellationToken)args[1]!);
            }));
            _services = services.BuildServiceProvider();
        }

        private async Task<ApiResult<object>> Execute(Func<CancellationToken, Task<ApiResult<object>>> action, CancellationToken ct)
        {
            Assert.False(_inTransaction);
            _inTransaction = true;
            try
            {
                var result = await action(ct);
                Commits++;
                return result;
            }
            finally
            {
                _inTransaction = false;
            }
        }

        private TRepository Repository<TRepository, TEntity>(Dictionary<Guid, TEntity> entities, Func<TEntity, Guid> id)
            where TRepository : class
        {
            return Proxy<TRepository>((method, args) =>
            {
                Assert.True(_inTransaction);
                if (method.Name == "AddAsync")
                {
                    var entity = (TEntity)args[0]!;
                    entities.Add(id(entity), entity);
                    return Task.FromResult(entity);
                }
                Assert.Contains(method.Name, new[] { "GetTrackedRequiredAsync", "GetRequiredByIdAsync" });
                return Task.FromResult(entities[(Guid)args[0]!]);
            });
        }

        public void Dispose()
        {
            _services.Dispose();
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object?[], object?> invoke)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, TestProxy>();
        ((TestProxy)(object)proxy).InvokeMethod = invoke;
        return proxy;
    }

    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> InvokeMethod { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => InvokeMethod(targetMethod!, args!);
    }
}