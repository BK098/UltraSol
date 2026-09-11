using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using UltraSol.Modules.Catalog.Application.Features.Bundles.Commands;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Shared.Application.Responses;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Catalog.Domain.Tests;

public class BundleTests
{
    private static ProductItem Item() => ProductItem.Create(Product.Create("Component"), SKU.Create(Guid.NewGuid().ToString()), []);

    [Fact]
    public void MutationsReplaceValueObjectAndPreserveInvariants()
    {
        var product = Product.Create("Bundle");
        var first = Item();
        var second = Item();
        var bundle = ProductItem.CreateBundle(product, SKU.Create("BUNDLE"), [], [(first, 1)]);
        var original = bundle.BundleDefinition!;
        bundle.AddBundleComponent(product, second, 2);
        Assert.Single(original.Components);
        Assert.Equal(2, bundle.BundleDefinition!.Components.Count);
        Assert.Throws<DomainException>(() => bundle.AddBundleComponent(product, second, 3));
        Assert.Throws<DomainException>(() => bundle.AddBundleComponent(product, bundle, 1));
        Assert.Throws<DomainException>(() => bundle.ChangeBundleComponentQuantity(product, first.Id, 0));
        Assert.Throws<DomainException>(() => bundle.RemoveBundleComponent(product, Guid.NewGuid()));
        bundle.ChangeBundleComponentQuantity(product, second.Id, 5);
        Assert.Equal(5, bundle.BundleDefinition.Components.Single(x => x.ProductItemId == second.Id).Quantity);
        bundle.RemoveBundleComponent(product, first.Id);
        Assert.Throws<DomainException>(() => bundle.RemoveBundleComponent(product, second.Id));
        Assert.Single(bundle.BundleDefinition.Components);
    }

    [Fact]
    public void OwnershipArchiveAndNestedBundlesAreRejected()
    {
        var product = Product.Create("Bundle");
        var component = Item();
        var bundle = ProductItem.CreateBundle(product, SKU.Create("BUNDLE"), [], [(component, 1)]);
        var otherProduct = Product.Create("Other");
        var nested = ProductItem.CreateBundle(otherProduct, SKU.Create("NESTED"), [], [(component, 1)]);
        Assert.Throws<DomainException>(() => bundle.AddBundleComponent(product, nested, 1));
        Assert.Throws<DomainException>(() => component.AddBundleComponent(Product.Create("Wrong"), Item(), 1));
        Assert.Throws<DomainException>(() => bundle.ChangeBundleComponentQuantity(otherProduct, component.Id, 2));
        var archived = Item();
        archived.Archive();
        Assert.Throws<DomainException>(() => bundle.AddBundleComponent(product, archived, 1));
        bundle.Archive();
        Assert.Throws<DomainException>(() => bundle.AddBundleComponent(product, Item(), 1));
        Assert.Throws<DomainException>(() => bundle.ChangeBundleComponentQuantity(product, component.Id, 2));
        Assert.Throws<DomainException>(() => bundle.RemoveBundleComponent(product, component.Id));
        otherProduct.Archive();
        Assert.Throws<DomainException>(() => nested.AddBundleComponent(otherProduct, Item(), 1));
    }

    [Fact]
    public void RegularItemCannotBecomeBundleByAddingComponent()
    {
        var product = Product.Create("Regular");
        var item = ProductItem.Create(product, SKU.Create("REGULAR"), []);
        Assert.Throws<DomainException>(() => item.AddBundleComponent(product, Item(), 1));
        Assert.False(item.IsBundle);
    }

    [Fact]
    public void ValidatorsRejectMissingBodyIdsAndNonPositiveQuantities()
    {
        var id = Guid.NewGuid();
        var add = new AddBundleComponentValidator();
        var change = new ChangeBundleComponentQuantityValidator();
        var remove = new RemoveBundleComponentValidator();
        Assert.False(add.Validate(new AddBundleComponentCommand(id, id, id, null)).IsValid);
        Assert.False(change.Validate(new ChangeBundleComponentQuantityCommand(id, id, id, null)).IsValid);
        foreach (var quantity in new[] { 0, -1 })
        {
            Assert.False(add.Validate(new AddBundleComponentCommand(id, id, id, new(quantity))).IsValid);
            Assert.False(change.Validate(new ChangeBundleComponentQuantityCommand(id, id, id, new(quantity))).IsValid);
        }
        foreach (var ids in new[] { (Guid.Empty, id, id), (id, Guid.Empty, id), (id, id, Guid.Empty) })
        {
            Assert.False(add.Validate(new AddBundleComponentCommand(ids.Item1, ids.Item2, ids.Item3, new(1))).IsValid);
            Assert.False(change.Validate(new ChangeBundleComponentQuantityCommand(ids.Item1, ids.Item2, ids.Item3, new(1))).IsValid);
            Assert.False(remove.Validate(new RemoveBundleComponentCommand(ids.Item1, ids.Item2, ids.Item3)).IsValid);
        }
        Assert.True(add.Validate(new AddBundleComponentCommand(id, id, id, new(1))).IsValid);
        Assert.True(change.Validate(new ChangeBundleComponentQuantityCommand(id, id, id, new(1))).IsValid);
        Assert.True(remove.Validate(new RemoveBundleComponentCommand(id, id, id)).IsValid);
    }

    [Fact]
    public async Task HandlersUseTrackedOwnerTransactionAndReturnBundleId()
    {
        var product = Product.Create("Bundle");
        var first = Item();
        var second = Item();
        var bundle = ProductItem.CreateBundle(product, SKU.Create("BUNDLE"), [], [(first, 1)]);
        var items = new Dictionary<Guid, ProductItem> { [bundle.Id] = bundle, [first.Id] = first, [second.Id] = second };
        var inTransaction = false;
        var commits = 0;
        async Task<ApiResult<object>> Execute(Func<CancellationToken, Task<ApiResult<object>>> action, CancellationToken ct)
        {
            inTransaction = true;
            try
            {
                var result = await action(ct);
                commits++;
                return result;
            }
            finally
            {
                inTransaction = false;
            }
        }
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<AddBundleComponentCommand>());
        services.AddSingleton(Proxy<IProductRepository>((method, args) =>
        {
            Assert.True(inTransaction);
            Assert.Equal("GetTrackedRequiredAsync", method.Name);
            Assert.Equal(product.Id, args[0]);
            return Task.FromResult(product);
        }));
        services.AddSingleton(Proxy<IProductItemRepository>((method, args) =>
        {
            Assert.True(inTransaction);
            var id = (Guid)args[0]!;
            Assert.Equal(id == bundle.Id ? "GetTrackedRequiredAsync" : "GetRequiredByIdAsync", method.Name);
            return Task.FromResult(items[id]);
        }));
        services.AddSingleton(Proxy<ICatalogUnitOfWork>((method, args) =>
        {
            Assert.Equal("ExecuteInTransactionAsync", method.Name);
            return Execute((Func<CancellationToken, Task<ApiResult<object>>>)args[0]!, (CancellationToken)args[1]!);
        }));
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var commands = new IRequest<ApiResult<object>>[]
        {
            new AddBundleComponentCommand(product.Id, bundle.Id, second.Id, new(2)),
            new ChangeBundleComponentQuantityCommand(product.Id, bundle.Id, second.Id, new(3)),
            new RemoveBundleComponentCommand(product.Id, bundle.Id, second.Id)
        };
        foreach (var command in commands)
        {
            var response = await sender.Send(command);
            Assert.Equal(200, response.StatusCode);
            Assert.Equal(bundle.Id, Assert.IsType<Guid>(response.Data));
        }
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sender.Send(new AddBundleComponentCommand(product.Id, bundle.Id, Guid.NewGuid(), new(1))));
        await Assert.ThrowsAsync<DomainException>(() => sender.Send(new RemoveBundleComponentCommand(product.Id, bundle.Id, first.Id)));
        Assert.Equal(3, commits);
    }

    private static T Proxy<T>(Func<MethodInfo, object?[], object?> invoke)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, CollectionTests.TestProxy>();
        ((CollectionTests.TestProxy)(object)proxy).InvokeMethod = invoke;
        return proxy;
    }
}