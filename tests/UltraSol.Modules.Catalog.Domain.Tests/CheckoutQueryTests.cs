using MediatR;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using UltraSol.Modules.Catalog.Application.Features.Checkout.Queries;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Shared.Application.Behaviors;
using Xunit;

namespace UltraSol.Modules.Catalog.Domain.Tests;

public sealed class CheckoutQueryTests
{
    [Fact]
    public async Task Checkout_preserves_missing_and_unsellable_items_with_bundle_components()
    {
        var activeId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var componentId = Guid.NewGuid();
        var reads = new CheckoutReads([
            new(activeId, Guid.NewGuid(), "SKU-1", "Product", "Color: Black", "item.png", true, null, false, []),
            new(bundleId, Guid.NewGuid(), "BUNDLE-1", "Bundle", "", "product.png", false, "BundleComponentNotSellable", true,
                [new(componentId, 2)])
        ]);
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IClientCatalogReadStore>(reads)
            .AddMediatR(options =>
            {
                options.RegisterServicesFromAssembly(typeof(GetCheckoutItemsQuery).Assembly);
                options.AddOpenBehavior(typeof(ValidationBehavior<,>));
            })
            .AddValidatorsFromAssemblyContaining<GetCheckoutItemsValidator>()
            .BuildServiceProvider();

        var result = await services.GetRequiredService<ISender>().Send(new GetCheckoutItemsQuery([activeId, missingId, bundleId]));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.Items.Count);
        Assert.Equal("Color: Black", result.Data.Items[0].VariantDescription);
        Assert.False(result.Data.Items[1].IsSellable);
        Assert.Equal("ProductItemNotFound", result.Data.Items[1].ReasonCode);
        Assert.Equal(missingId, result.Data.Items[1].ProductItemId);
        Assert.False(result.Data.Items[2].IsSellable);
        Assert.Equal(componentId, Assert.Single(result.Data.Items[2].Components).ProductItemId);
    }

    private sealed class CheckoutReads(IReadOnlyList<CheckoutItemData> items) : IClientCatalogReadStore
    {
        public Task<IReadOnlyList<CheckoutItemData>> CheckoutItemsAsync(IReadOnlyCollection<Guid> productItemIds, CancellationToken ct) =>
            Task.FromResult(items);
        public Task<UltraSol.Shared.Domain.Common.Paging.PaginatedResult<ClientProduct>> ProductsAsync(
            UltraSol.Shared.Domain.Common.Paging.PaginationRequest page, CancellationToken ct) => throw new NotSupportedException();
        public Task<ClientProductDetail> ProductAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}