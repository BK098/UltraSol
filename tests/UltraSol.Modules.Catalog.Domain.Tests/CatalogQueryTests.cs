using System.Reflection;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using UltraSol.Modules.Catalog.Application.Features.Products.Queries;
using UltraSol.Modules.Catalog.Application.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Application.Behaviors;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Paging;
using Xunit;

namespace UltraSol.Modules.Catalog.Domain.Tests;

public class CatalogQueryTests
{
    private static readonly Guid Id = Guid.Parse("d2fb882b-ff47-48af-b9a0-899f642917cb");
    private static readonly Assembly Application = typeof(GetProductsQuery).Assembly;
    public static IEnumerable<object[]> Queries => Application.GetTypes()
        .Where(t => t.IsPublic && t.Name.StartsWith("Get", StringComparison.Ordinal) && t.Name.EndsWith("Query", StringComparison.Ordinal))
        .Select(t => new object[] { t });

    private static object Request(Type type, string? invalidProperty = null, object? invalidValue = null)
    {
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        return constructor.Invoke(constructor.GetParameters().Select(p =>
        {
            if (p.Name == invalidProperty)
            {
                return invalidValue;
            }
            if (p.ParameterType == typeof(Guid))
            {
                return (object)Id;
            }
            if (p.ParameterType == typeof(Guid[]))
            {
                return new[] { Id };
            }
            if (p.ParameterType == typeof(PagedFilter))
            {
                return new PagedFilter();
            }
            if (p.ParameterType == typeof(bool))
            {
                return false;
            }
            return null;
        }).ToArray());
    }

    private static ServiceProvider Services(bool missing = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(Application);
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(Application);
        var proxy = DispatchProxy.Create<ICatalogReadStore, CollectionTests.TestProxy>();
        ((CollectionTests.TestProxy)(object)proxy).InvokeMethod = (method, args) =>
        {
            if (missing)
            {
                throw new KeyNotFoundException("Missing catalog resource.");
            }
            var named = new NamedData(Id, "Product name");
            var media = new MediaData(Id, "https://cdn.example.com/image.png", "Main image", 0, true);
            var variation = new VariationData
            {
                Id = Id, Name = "Color", Product = named, OptionCount = 1, Options = [new(Id, "Black")]
            };
            var product = new ProductData(Id, "Product name", "Full description", ProductStatus.Draft, new(Id, "Brand name"),
                media.Url, [new(Id, "Category name")], [media], [variation]);
            var item = new ItemData(Id, "SKU-001", ProductItemStatus.Inactive, named, true, media.Url,
                [new(Id, "Color", Id, "Black")], [media], [new(Id, "COMPONENT-001", named, "Active", 2, media.Url)]);
            var brand = new BrandData { Id = Id, Name = "Brand name", Description = "Brand description", IsArchived = true };
            var category = new CategoryData { Id = Id, Name = "Category name", Description = "Category description", Parent = new(Id, "Parent category") };
            var collection = new CollectionData
            {
                Id = Id, Name = "Collection name", Description = "Collection description", Type = CollectionType.Automatic,
                Status = CollectionStatus.Unpublished, MatchMode = RuleMatchMode.All, Brands = [new(Id, "Brand name")], Categories = [new(Id, "Category name")]
            };
            var page = PaginationRequest.Create(1, 10);
            return method.Name switch
            {
                "ProductsAsync" => Task.FromResult(PaginatedResult<ProductData>.Create([product], 1, page)),
                "ProductAsync" => Task.FromResult(product),
                "ItemsAsync" => Task.FromResult(PaginatedResult<ItemData>.Create([item], 1, page)),
                "ItemAsync" => Task.FromResult(item),
                "BrandsAsync" => Task.FromResult(PaginatedResult<BrandData>.Create([brand], 1, page)),
                "BrandAsync" => Task.FromResult(brand),
                "CategoriesAsync" => Task.FromResult(PaginatedResult<CategoryData>.Create([category], 1, page)),
                "CategoryAsync" => Task.FromResult(category),
                "CollectionsAsync" => Task.FromResult(PaginatedResult<CollectionData>.Create([collection], 1, page)),
                "CollectionAsync" => Task.FromResult(collection),
                "VariationsAsync" => Task.FromResult(PaginatedResult<VariationData>.Create([variation], 1, page)),
                "VariationAsync" => Task.FromResult(variation),
                _ => throw new NotSupportedException(method.Name)
            };
        };
        services.AddSingleton(proxy);
        var client = DispatchProxy.Create<IClientCatalogReadStore, CollectionTests.TestProxy>();
        ((CollectionTests.TestProxy)(object)client).InvokeMethod = (method, args) =>
        {
            if (missing)
            {
                throw new KeyNotFoundException("Missing catalog resource.");
            }
            var product = new ClientProduct(Id, "Product name", "Description", null);
            return method.Name switch
            {
                "ProductsAsync" => Task.FromResult(PaginatedResult<ClientProduct>.Create([product], 1, PaginationRequest.Create(1, 10))),
                "ProductAsync" => Task.FromResult(new ClientProductDetail(product, [new(Id, "SKU-001", null)])),
                "CheckoutItemsAsync" => Task.FromResult<IReadOnlyList<CheckoutItemData>>([
                    new(Id, Id, "SKU-001", "Product name", "Color: Black", null, true, null, false, [])
                ]),
                _ => throw new NotSupportedException(method.Name)
            };
        };
        services.AddSingleton(client);
        return services.BuildServiceProvider();
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task EveryQueryResolvesAndOwnsAllItsResponseTypes(Type queryType)
    {
        using var services = Services();
        var result = await services.GetRequiredService<ISender>().Send(Request(queryType));
        var responseType = queryType.GetNestedType("Response")!;
        Assert.NotNull(responseType);
        CheckResponseTypes(responseType, queryType);
        var data = result!.GetType().GetProperty("Data")!.GetValue(result)!;
        var json = JsonSerializer.Serialize(data);
        Assert.DoesNotContain("OptionSignature", json);
        Assert.DoesNotContain("ConcurrencyStamp", json);
        if (queryType.Name.Contains("ProductItems") || queryType.Name.Contains("ProductItemDetail") || queryType.Name == "GetBundlesQuery")
        {
            Assert.Contains("Product name", json);
            Assert.Contains("Color", json);
            Assert.Contains("Black", json);
            Assert.Contains("Inactive", json);
        }
        if (queryType.Name == "GetProductDetailQuery")
        {
            Assert.Contains("Full description", json);
            Assert.Contains("Brand name", json);
            Assert.Contains("Category name", json);
        }
        if (queryType.Name == "GetProductsQuery")
        {
            Assert.DoesNotContain("Full description", json);
            Assert.DoesNotContain("Variations", json);
        }
    }

    private static void CheckResponseTypes(Type type, Type owner)
    {
        foreach (var property in type.GetProperties())
        {
            var fieldType = property.PropertyType;
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                fieldType = fieldType.GetGenericArguments()[0];
            }
            if (fieldType.IsNested)
            {
                Assert.Equal(owner, fieldType.DeclaringType);
                CheckResponseTypes(fieldType, owner);
            }
            Assert.NotEqual(typeof(ICatalogReadStore).Namespace, fieldType.Namespace);
        }
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task EveryQueryValidatesIdsFilterAndEnums(Type queryType)
    {
        using var services = Services();
        var sender = services.GetRequiredService<ISender>();
        foreach (var property in queryType.GetProperties())
        {
            if (property.PropertyType == typeof(Guid) || property.PropertyType == typeof(Guid?))
            {
                await Assert.ThrowsAsync<ValidationException>(() => sender.Send(Request(queryType, property.Name, Guid.Empty)));
            }
            if (property.PropertyType == typeof(PagedFilter))
            {
                await Assert.ThrowsAsync<ValidationException>(() => sender.Send(Request(queryType, property.Name, null)));
                await Assert.ThrowsAsync<ValidationException>(() => sender.Send(Request(queryType, property.Name, new PagedFilter { PageSize = 201 })));
                await Assert.ThrowsAsync<ValidationException>(() => sender.Send(Request(queryType, property.Name, new PagedFilter { PageIndex = 0 })));
            }
            var enumType = Nullable.GetUnderlyingType(property.PropertyType);
            if (enumType?.IsEnum == true)
            {
                await Assert.ThrowsAsync<ValidationException>(() => sender.Send(Request(queryType, property.Name, Enum.ToObject(enumType, 999))));
            }
        }
    }

    [Fact]
    public async Task ChildDetailRejectsAnIdOutsideItsOwner()
    {
        using var services = Services();
        var type = Application.GetType("UltraSol.Modules.Catalog.Application.Features.Variations.Queries.GetVariationOptionDetailQuery")!;
        await Assert.ThrowsAsync<EntityNotFoundException>(() => services.GetRequiredService<ISender>().Send(Request(type, "OptionId", Guid.NewGuid())));
    }

    [Fact]
    public async Task MissingParentIsNotAnEmptySuccess()
    {
        using var services = Services(missing: true);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => services.GetRequiredService<ISender>().Send(new GetProductDetailQuery(Id)));
    }

    [Fact]
    public async Task ChildPaginationSearchPreservesTotalAndContext()
    {
        using var services = Services();
        var type = Application.GetType("UltraSol.Modules.Catalog.Application.Features.Products.Queries.GetProductMediaQuery")!;
        var result = await services.GetRequiredService<ISender>().Send(Request(type, "Filter", new PagedFilter { PageIndex = 2, PageSize = 1, Search = "MAIN" }));
        var data = result!.GetType().GetProperty("Data")!.GetValue(result)!;
        Assert.Equal(1, data.GetType().GetProperty("TotalCount")!.GetValue(data));
        Assert.Empty((System.Collections.IEnumerable)data.GetType().GetProperty("Items")!.GetValue(data)!);
    }
}