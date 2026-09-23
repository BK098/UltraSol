using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Infrastructure.Repositories;
using UltraSol.Modules.Pricing.Infrastructure.Repositories;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Modules.Pricing.Application.Integrations.Catalog;
using UltraSol.Shared.Application.Responses;
using Xunit;

namespace UltraSol.Modules.Pricing.Tests;

public sealed class PricingHttpTests(PricingDatabaseFixture fixture) : IClassFixture<PricingDatabaseFixture>
{
    [PricingPostgresFact]
    public async Task Catalog_lookup_completes_before_the_pricing_transaction_starts()
    {
        var sku = await CreateDraftSku();
        await using var host = await PricingHttpHost.Start(fixture.Connection, services =>
            services.AddScoped<ICatalogSkuClient, TransactionCheckingCatalogClient>());
        var list = await Create(host.Client, "price-lists", new { name = "Lookup before transaction", currency = "VND", type = 1 });
        await Create(host.Client, "sku-prices", new { priceListId = list, skuId = sku.Id });
        await Create(host.Client, "negotiated-prices", new
        {
            customerId = Guid.NewGuid(), skuId = sku.Id, transactionId = Guid.NewGuid(), quantity = 10, amount = 80_000,
            currency = "VND", channel = 1, reason = "Agreed", effectiveFrom = host.Clock.Now, effectiveTo = host.Clock.Now.AddDays(1)
        });
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(503)]
    public async Task Failed_catalog_lookup_returns_without_opening_the_pricing_database(int status)
    {
        var lookup = status == 404 ? ApiResultBuilder.Success(false)
            : ApiResultBuilder.Error<bool>("Catalog lookup failed.", status, new Dictionary<string, string[]> { ["Code"] = ["CatalogUnavailable"] });
        await using var host = await PricingHttpHost.Start("Host=localhost;Port=1;Database=unused;Username=unused;Timeout=1", services =>
            services.AddSingleton<ICatalogSkuClient>(new FailedCatalogClient(lookup)));
        using var configured = await host.Client.PostAsJsonAsync("api/pricing/sku-prices", new { priceListId = Guid.NewGuid(), skuId = Guid.NewGuid() });
        using var negotiated = await host.Client.PostAsJsonAsync("api/pricing/negotiated-prices", new
        {
            customerId = Guid.NewGuid(), skuId = Guid.NewGuid(), transactionId = Guid.NewGuid(), quantity = 10, amount = 80_000,
            currency = "VND", channel = 1, reason = "Agreed", effectiveFrom = host.Clock.Now, effectiveTo = host.Clock.Now.AddDays(1)
        });
        foreach (var response in new[] { configured, negotiated })
        {
            Assert.Equal(status, (int)response.StatusCode);
            var result = await Body(response);
            Assert.False(result.GetProperty("isSuccess").GetBoolean());
            if (status == 503)
            {
                Assert.Equal("CatalogUnavailable", result.GetProperty("errors").GetProperty("Code")[0].GetString());
            }
        }
    }

    private sealed class FailedCatalogClient(ApiResult<bool> result) : ICatalogSkuClient
    {
        public Task<ApiResult<bool>> ExistsAsync(Guid skuId, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class TransactionCheckingCatalogClient(PricingDbContext db) : ICatalogSkuClient
    {
        public Task<ApiResult<bool>> ExistsAsync(Guid skuId, CancellationToken cancellationToken)
        {
            Assert.Null(db.Database.CurrentTransaction);
            return Task.FromResult(ApiResultBuilder.Success(true));
        }
    }

    [Fact]
    public async Task Catalog_lookup_requires_authentication_permission_and_nonempty_sku()
    {
        await using var host = await PricingHttpHost.Start();
        var url = $"api/catalog/skus/{Guid.NewGuid()}/exists";
        host.Client.DefaultRequestHeaders.Add("X-Test-Deny", "Catalog.ProductItems.SkuExists");
        using var denied = await host.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        host.Client.DefaultRequestHeaders.Remove("X-Test-Deny");
        using var invalid = await host.Client.GetAsync($"api/catalog/skus/{Guid.Empty}/exists");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await host.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Catalog_checkout_requires_authentication_permission_and_valid_items()
    {
        await using var host = await PricingHttpHost.Start();
        host.Client.DefaultRequestHeaders.Add("X-Test-Deny", "Catalog.Checkout.GetCheckoutItems");
        using var denied = await host.Client.PostAsJsonAsync("api/catalog/checkout/items", new
        {
            productItemIds = new[] { Guid.NewGuid() }
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        host.Client.DefaultRequestHeaders.Remove("X-Test-Deny");
        using var invalid = await host.Client.PostAsJsonAsync("api/catalog/checkout/items", new
        {
            productItemIds = new[] { Guid.Empty }
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await host.Client.PostAsJsonAsync("api/catalog/checkout/items", new
        {
            productItemIds = new[] { Guid.NewGuid() }
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task All_pricing_routes_require_authentication_and_have_distinct_openapi_schemas()
    {
        await using var host = await PricingHttpHost.Start();
        host.Client.DefaultRequestHeaders.Authorization = null;
        var endpoints = host.App.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("api/pricing/", StringComparison.Ordinal)).ToArray();
        Assert.Equal(37, endpoints.Length);
        foreach (var endpoint in endpoints)
        {
            Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
            var path = Regex.Replace(endpoint.RoutePattern.RawText!, "\\{[^}]+\\}", Guid.NewGuid().ToString());
            var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single();
            using var response = await host.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var result = await Body(response);
            Assert.Equal(401, result.GetProperty("statusCode").GetInt32());
        }
        var document = host.App.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        Assert.Contains("GetPriceListsQueryResponse", document.Components!.Schemas!.Keys);
        Assert.Contains("GetSkuPriceDetailQueryResponse", document.Components.Schemas.Keys);
        Assert.Contains("/api/pricing/resolve", document.Paths.Keys);
        Assert.Contains("/api/pricing/quotes", document.Paths.Keys);
    }

    [Fact]
    public async Task Quote_endpoint_requires_authentication_permission_and_valid_body_before_database_access()
    {
        await using var host = await PricingHttpHost.Start();
        host.Client.DefaultRequestHeaders.Add("X-Test-Deny", "Pricing.Quotes.CreatePriceQuote");
        using var denied = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "Retail", currency = "VND", lines = new[] { new { productItemId = Guid.NewGuid(), quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        host.Client.DefaultRequestHeaders.Remove("X-Test-Deny");
        using var invalid = await host.Client.PostAsJsonAsync<object?>("api/pricing/quotes", null);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var numericType = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "2", currency = "VND", lines = new[] { new { productItemId = Guid.NewGuid(), quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, numericType.StatusCode);
        using var nullLine = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "Retail", currency = "VND", lines = new object?[] { null }
        });
        Assert.Equal(HttpStatusCode.BadRequest, nullLine.StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "Retail", currency = "VND", lines = new[] { new { productItemId = Guid.NewGuid(), quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Null_bodies_invalid_context_and_missing_approval_permission_stop_before_database_access()
    {
        await using var host = await PricingHttpHost.Start();
        foreach (var path in new[] { "price-lists", "sku-prices", "contracts", "negotiated-prices", "resolve",
            $"sku-prices/{Guid.NewGuid()}/initial-price", $"sku-prices/{Guid.NewGuid()}/amendments/price-change" })
        {
            using var response = await host.Client.PostAsJsonAsync<object?>("api/pricing/" + path, null);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False((await Body(response)).GetProperty("isSuccess").GetBoolean());
        }
        using var invalid = await host.Client.PostAsJsonAsync("api/pricing/resolve", new { skuId = Guid.Empty, quantity = 0, currency = "bad!", channel = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var invalidPage = await host.Client.GetAsync("api/pricing/price-lists?pageIndex=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
        foreach (var action in new[] { "submit", "approve", "reject", "revoke" })
        {
            host.Client.DefaultRequestHeaders.Remove("X-Test-Deny");
            host.Client.DefaultRequestHeaders.Add("X-Test-Deny", $"Pricing.NegotiatedPrices.{char.ToUpperInvariant(action[0])}{action[1..]}NegotiatedPrice");
            using var response = await host.Client.PostAsync($"api/pricing/negotiated-prices/{Guid.NewGuid()}/{action}", null);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(403, (await Body(response)).GetProperty("statusCode").GetInt32());
        }
    }

    [PricingPostgresFact]
    public async Task Configured_price_http_flow_uses_catalog_http_and_preserves_unattributed_audit()
    {
        var sku = await CreateDraftSku();
        await using var host = await PricingHttpHost.Start(fixture.Connection);
        var list = await Create(host.Client, "price-lists", new { name = "HTTP wholesale", currency = "vnd", type = 1, actorId = Guid.NewGuid() });
        var price = await Create(host.Client, "sku-prices", new { priceListId = list, skuId = sku.Id });
        var initial = await Create(host.Client, $"sku-prices/{price}/initial-price", new { tiers = new[] { new { minimumQuantity = 1, amount = 100_000 }, new { minimumQuantity = 50, amount = 85_000 } } });
        using var missingSku = await host.Client.PostAsJsonAsync("api/pricing/sku-prices", new { priceListId = list, skuId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, missingSku.StatusCode);
        using var duplicate = await host.Client.PostAsJsonAsync("api/pricing/sku-prices", new { priceListId = list, skuId = sku.Id });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var invalidTiers = await host.Client.PostAsJsonAsync($"api/pricing/sku-prices/{price}/change-now", new { tiers = new[] { new { minimumQuantity = 1, amount = 100 }, new { minimumQuantity = 1, amount = 90 } } });
        Assert.Equal(HttpStatusCode.BadRequest, invalidTiers.StatusCode);
        await using (var catalog = fixture.Catalog())
        {
            await new CatalogUnitOfWork(catalog, new PricingTestDispatcher()).ExecuteInTransactionAsync(async ct =>
            {
                var item = await new ProductItemRepository(catalog).GetTrackedRequiredAsync(sku.Id, ct);
                item.ChangeSku(SKU.Create("RENAMED-" + Guid.NewGuid().ToString("N")));
            });
        }
        using var resolved = await host.Client.PostAsJsonAsync("api/pricing/resolve", new { skuId = sku.Id, quantity = 60, currency = "VND", channel = 1, priceListId = list });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var snapshot = (await Body(resolved)).GetProperty("data");
        Assert.Equal(85_000, snapshot.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(initial, snapshot.GetProperty("pricePeriodId").GetGuid());
        using var filtered = await host.Client.GetAsync("api/pricing/price-lists?search=HTTP%20wholesale&type=1&currency=vnd&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        Assert.Equal(list, (await Body(filtered)).GetProperty("data").GetProperty("items")[0].GetProperty("id").GetGuid());
        using var page = await host.Client.GetAsync($"api/pricing/sku-prices/{price}/history?pageIndex=1&pageSize=1");
        var history = (await Body(page)).GetProperty("data");
        Assert.Equal(1, history.GetProperty("totalCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, history.GetProperty("items")[0].GetProperty("actorId").ValueKind);
        await using var check = fixture.Create();
        var storedList = await new PriceListRepository(check).GetRequiredByIdAsync(list);
        Assert.Null(storedList.CreatedBy);
        Assert.Equal(host.Clock.Now, storedList.CreatedAt);
    }

    [PricingPostgresFact]
    public async Task Retail_quote_uses_configured_current_price_and_returns_complete_totals_and_provenance()
    {
        var sku = await CreateDraftSku();
        await using var host = await PricingHttpHost.Start(fixture.Connection);
        var list = await Create(host.Client, "price-lists", new { name = "Retail quote", currency = "VND", type = 0 });
        var price = await Create(host.Client, "sku-prices", new { priceListId = list, skuId = sku.Id });
        var period = await Create(host.Client, $"sku-prices/{price}/initial-price", new
        {
            tiers = new[] { new { minimumQuantity = 1, amount = 125_000 } }
        });
        using var missing = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "Retail", currency = "VND", lines = new[] { new { productItemId = sku.Id, quantity = 2 } }
        });
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        Assert.Equal("DefaultPriceListNotConfigured", (await Body(missing)).GetProperty("errors").GetProperty("Code")[0].GetString());
        host.App.Configuration["Pricing:DefaultPriceLists:Retail:VND"] = list.ToString();

        using var response = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "Retail", currency = "vnd", lines = new[] { new { productItemId = sku.Id, quantity = 2 } }
        });

        var responseBody = await Body(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody.ToString());
        var quote = responseBody.GetProperty("data");
        Assert.NotEqual(Guid.Empty, quote.GetProperty("quoteId").GetGuid());
        Assert.Equal(host.Clock.Now, quote.GetProperty("quotedAt").GetDateTimeOffset());
        Assert.Equal("VND", quote.GetProperty("currency").GetString());
        Assert.Equal(250_000, quote.GetProperty("subtotal").GetDecimal());
        Assert.Equal(250_000, quote.GetProperty("grandTotal").GetDecimal());
        Assert.Equal(0, quote.GetProperty("discountTotal").GetDecimal());
        Assert.Equal(0, quote.GetProperty("taxTotal").GetDecimal());
        Assert.Equal(0, quote.GetProperty("shippingAmount").GetDecimal());
        var line = quote.GetProperty("lines")[0];
        Assert.Equal(125_000, line.GetProperty("listUnitPrice").GetDecimal());
        Assert.Equal(125_000, line.GetProperty("finalUnitPrice").GetDecimal());
        Assert.Equal(250_000, line.GetProperty("lineTotal").GetDecimal());
        Assert.Equal("PriceList", line.GetProperty("priceSource").GetString());
        Assert.Equal(list, line.GetProperty("priceListId").GetGuid());
        Assert.Equal(price, line.GetProperty("skuPriceId").GetGuid());
        Assert.Equal(period, line.GetProperty("pricePeriodId").GetGuid());
    }

    [PricingPostgresFact]
    public async Task Wholesale_contract_and_negotiated_quotes_preserve_commercial_context()
    {
        var wholesaleSku = await CreateDraftSku();
        var contractSku = await CreateDraftSku();
        await using var host = await PricingHttpHost.Start(fixture.Connection);
        var wholesaleList = await Create(host.Client, "price-lists", new { name = "Wholesale quote", currency = "VND", type = 1 });
        var wholesalePrice = await Create(host.Client, "sku-prices", new { priceListId = wholesaleList, skuId = wholesaleSku.Id });
        await Create(host.Client, $"sku-prices/{wholesalePrice}/initial-price", new
        {
            tiers = new[] { new { minimumQuantity = 1, amount = 100_000 }, new { minimumQuantity = 10, amount = 90_000 } }
        });
        host.App.Configuration["Pricing:DefaultPriceLists:Wholesale:VND"] = wholesaleList.ToString();
        using var wholesaleResponse = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "Wholesale", currency = "VND", lines = new[] { new { productItemId = wholesaleSku.Id, quantity = 10 } }
        });
        var wholesaleBody = await Body(wholesaleResponse);
        Assert.True(wholesaleResponse.StatusCode == HttpStatusCode.OK, wholesaleBody.ToString());
        var wholesaleLine = wholesaleBody.GetProperty("data").GetProperty("lines")[0];
        Assert.Equal("PriceList", wholesaleLine.GetProperty("priceSource").GetString());
        Assert.Equal(90_000, wholesaleLine.GetProperty("finalUnitPrice").GetDecimal());

        var customer = Guid.NewGuid();
        var contractList = await Create(host.Client, "price-lists", new { name = "Contract quote", currency = "VND", type = 2 });
        var contract = await Create(host.Client, "contracts", new
        {
            customerId = customer, priceListId = contractList, effectiveFrom = host.Clock.Now,
            effectiveTo = host.Clock.Now.AddYears(1), commercialTerms = "Net 30"
        });
        var contractPrice = await Create(host.Client, "sku-prices", new { priceListId = contractList, skuId = contractSku.Id });
        var contractPeriod = await Create(host.Client, $"sku-prices/{contractPrice}/initial-price", new
        {
            tiers = new[] { new { minimumQuantity = 1, amount = 75_000 } }
        });
        await Ok(host.Client.PostAsync($"api/pricing/contracts/{contract}/activate", null));
        using var contractResponse = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "Contract", currency = "VND", customerId = customer, contractId = contract,
            lines = new[] { new { productItemId = contractSku.Id, quantity = 2 } }
        });
        Assert.Equal(HttpStatusCode.OK, contractResponse.StatusCode);
        var contractLine = (await Body(contractResponse)).GetProperty("data").GetProperty("lines")[0];
        Assert.Equal("Contract", contractLine.GetProperty("priceSource").GetString());
        Assert.Equal(contract, contractLine.GetProperty("contractId").GetGuid());
        Assert.Equal(contractPeriod, contractLine.GetProperty("pricePeriodId").GetGuid());

        var transaction = Guid.NewGuid();
        var negotiated = await Create(host.Client, "negotiated-prices", new
        {
            customerId = customer, skuId = wholesaleSku.Id, transactionId = transaction, quantity = 10, amount = 80_000,
            currency = "VND", channel = 1, reason = "Agreed", effectiveFrom = host.Clock.Now, effectiveTo = host.Clock.Now.AddDays(30)
        });
        await Ok(host.Client.PostAsync($"api/pricing/negotiated-prices/{negotiated}/submit", null));
        await Ok(host.Client.PostAsync($"api/pricing/negotiated-prices/{negotiated}/approve", null));
        using var negotiatedResponse = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "Wholesale", currency = "VND", customerId = customer, negotiationTransactionRef = transaction,
            lines = new[] { new { productItemId = wholesaleSku.Id, quantity = 10, negotiatedPriceId = negotiated } }
        });
        Assert.Equal(HttpStatusCode.OK, negotiatedResponse.StatusCode);
        var negotiatedLine = (await Body(negotiatedResponse)).GetProperty("data").GetProperty("lines")[0];
        Assert.Equal("Negotiated", negotiatedLine.GetProperty("priceSource").GetString());
        Assert.Equal(80_000, negotiatedLine.GetProperty("finalUnitPrice").GetDecimal());
        Assert.Equal(negotiated, negotiatedLine.GetProperty("negotiatedPriceId").GetGuid());
        Assert.Equal(JsonValueKind.Null, negotiatedLine.GetProperty("skuPriceId").ValueKind);

        using var wrongContext = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "Wholesale", currency = "VND", customerId = customer, negotiationTransactionRef = Guid.NewGuid(),
            lines = new[] { new { productItemId = wholesaleSku.Id, quantity = 10, negotiatedPriceId = negotiated } }
        });
        Assert.Equal(HttpStatusCode.Conflict, wrongContext.StatusCode);
        Assert.Equal("NegotiatedTransactionMismatch", (await Body(wrongContext)).GetProperty("errors").GetProperty("Code")[0].GetString());

        host.App.Configuration["Pricing:DefaultPriceLists:Wholesale:VND"] = contractList.ToString();
        using var wrongDefault = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "Wholesale", currency = "VND", lines = new[] { new { productItemId = wholesaleSku.Id, quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.Conflict, wrongDefault.StatusCode);
        Assert.Equal("PriceListTypeMismatch", (await Body(wrongDefault)).GetProperty("errors").GetProperty("Code")[0].GetString());
        host.App.Configuration["Pricing:DefaultPriceLists:Wholesale:USD"] = wholesaleList.ToString();
        using var wrongCurrency = await host.Client.PostAsJsonAsync("api/pricing/quotes", new
        {
            orderType = "Wholesale", currency = "USD", lines = new[] { new { productItemId = wholesaleSku.Id, quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.Conflict, wrongCurrency.StatusCode);
        Assert.Equal("PriceListCurrencyMismatch", (await Body(wrongCurrency)).GetProperty("errors").GetProperty("Code")[0].GetString());
    }

    [PricingPostgresFact]
    public async Task Catalog_checkout_reports_missing_unpublished_and_broken_bundle_items_without_dropping_lines()
    {
        var activeProduct = Product.Create("Active product");
        activeProduct.AddMedia("https://cdn.example.com/product.png");
        var variation = activeProduct.AddVariation("Color");
        var option = activeProduct.AddVariationOption(variation, "Black");
        var activeItem = ProductItem.Create(activeProduct, SKU.Create("ACTIVE-" + Guid.NewGuid().ToString("N")),
            [new OptionSelection(variation, option)]);
        activeItem.AddMedia(activeProduct, "https://cdn.example.com/item.png");
        activeItem.Activate();
        activeProduct.Publish();

        var unpublishedProduct = Product.Create("Unpublished product");
        var unpublishedItem = ProductItem.Create(unpublishedProduct, SKU.Create("UNPUBLISHED-" + Guid.NewGuid().ToString("N")), []);
        unpublishedItem.Activate();
        unpublishedProduct.Publish();
        unpublishedProduct.Unpublish();

        var componentProduct = Product.Create("Component product");
        var componentItem = ProductItem.Create(componentProduct, SKU.Create("COMPONENT-" + Guid.NewGuid().ToString("N")), []);
        componentProduct.Publish();

        var bundleProduct = Product.Create("Bundle product");
        var bundleItem = ProductItem.CreateBundle(bundleProduct, SKU.Create("BUNDLE-" + Guid.NewGuid().ToString("N")), [], [(componentItem, 2)]);
        bundleItem.Activate();
        bundleProduct.Publish();
        await using (var db = fixture.Catalog())
        {
            await new CatalogUnitOfWork(db, new PricingTestDispatcher()).ExecuteInTransactionAsync(async ct =>
            {
                var products = new ProductRepository(db);
                var items = new ProductItemRepository(db);
                foreach (var product in new[] { activeProduct, unpublishedProduct, componentProduct, bundleProduct })
                {
                    await products.AddAsync(product, ct);
                }
                foreach (var item in new[] { activeItem, unpublishedItem, componentItem, bundleItem })
                {
                    await items.AddAsync(item, ct);
                }
            });
        }
        var missingId = Guid.NewGuid();
        await using var host = await PricingHttpHost.Start(fixture.Connection);

        using var response = await host.Client.PostAsJsonAsync("api/catalog/checkout/items", new
        {
            productItemIds = new[] { activeItem.Id, unpublishedItem.Id, missingId, bundleItem.Id }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await Body(response)).GetProperty("data").GetProperty("items");
        Assert.Equal(4, rows.GetArrayLength());
        Assert.True(rows[0].GetProperty("isSellable").GetBoolean());
        Assert.Equal("Color: Black", rows[0].GetProperty("variantDescription").GetString());
        Assert.Equal("https://cdn.example.com/item.png", rows[0].GetProperty("imageUrl").GetString());
        Assert.False(rows[1].GetProperty("isSellable").GetBoolean());
        Assert.Equal("ProductNotPublished", rows[1].GetProperty("reasonCode").GetString());
        Assert.Equal(missingId, rows[2].GetProperty("productItemId").GetGuid());
        Assert.Equal("ProductItemNotFound", rows[2].GetProperty("reasonCode").GetString());
        Assert.False(rows[3].GetProperty("isSellable").GetBoolean());
        Assert.Equal("BundleComponentNotSellable", rows[3].GetProperty("reasonCode").GetString());
        Assert.True(rows[3].GetProperty("isBundle").GetBoolean());
        Assert.Equal(componentItem.Id, rows[3].GetProperty("components")[0].GetProperty("productItemId").GetGuid());
        Assert.Equal(2, rows[3].GetProperty("components")[0].GetProperty("quantity").GetInt32());
    }

    [PricingPostgresFact]
    public async Task Negotiated_price_requires_permission_and_exact_context_without_fallback()
    {
        var sku = await CreateDraftSku();
        await using var host = await PricingHttpHost.Start(fixture.Connection);
        var list = await Create(host.Client, "price-lists", new { name = "Negotiation", currency = "VND", type = 1 });
        var customer = Guid.NewGuid();
        var transaction = Guid.NewGuid();
        var negotiated = await Create(host.Client, "negotiated-prices", new
        {
            customerId = customer, skuId = sku.Id, transactionId = transaction, quantity = 60, amount = 77_000,
            currency = "VND", channel = 1, reason = "Agreed", effectiveFrom = host.Clock.Now, effectiveTo = host.Clock.Now.AddDays(30)
        });
        var context = new { skuId = sku.Id, quantity = 60, currency = "VND", channel = 1, priceListId = list,
            customerId = customer, transactionId = transaction, negotiatedPriceId = negotiated };
        using var draft = await host.Client.PostAsJsonAsync("api/pricing/resolve", context);
        Assert.Equal(HttpStatusCode.Conflict, draft.StatusCode);
        Assert.True((await Body(draft)).GetProperty("errors").TryGetProperty("Code", out _));
        await Ok(host.Client.PostAsync($"api/pricing/negotiated-prices/{negotiated}/submit", null));
        host.Client.DefaultRequestHeaders.Add("X-Test-Deny", "Pricing.NegotiatedPrices.ApproveNegotiatedPrice");
        using var denied = await host.Client.PostAsync($"api/pricing/negotiated-prices/{negotiated}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        host.Client.DefaultRequestHeaders.Remove("X-Test-Deny");
        await Ok(host.Client.PostAsync($"api/pricing/negotiated-prices/{negotiated}/approve", null));
        using var approved = await host.Client.PostAsJsonAsync("api/pricing/resolve", context);
        Assert.Equal(77_000, (await Body(approved)).GetProperty("data").GetProperty("unitPrice").GetDecimal());
        using var wrong = await host.Client.PostAsJsonAsync("api/pricing/resolve", new { skuId = sku.Id, quantity = 60, currency = "VND", channel = 1,
            priceListId = list, customerId = customer, transactionId = Guid.NewGuid(), negotiatedPriceId = negotiated });
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
        host.Clock.Now = host.Clock.Now.AddDays(1);
        await Ok(host.Client.PostAsync($"api/pricing/negotiated-prices/{negotiated}/revoke", null));
        using var revoked = await host.Client.PostAsJsonAsync("api/pricing/resolve", context);
        Assert.Equal(HttpStatusCode.Conflict, revoked.StatusCode);
        using var absent = await host.Client.PostAsJsonAsync("api/pricing/resolve", new { skuId = sku.Id, quantity = 60, currency = "VND", channel = 1,
            priceListId = list, customerId = customer, transactionId = transaction, negotiatedPriceId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
    }

    [PricingPostgresFact]
    public async Task Contract_price_uses_owned_list_and_missing_price_keeps_domain_code()
    {
        await using var host = await PricingHttpHost.Start(fixture.Connection);
        var customer = Guid.NewGuid();
        var list = await Create(host.Client, "price-lists", new { name = "Contract", currency = "VND", type = 2 });
        var contract = await Create(host.Client, "contracts", new { customerId = customer, priceListId = list,
            effectiveFrom = host.Clock.Now, effectiveTo = host.Clock.Now.AddYears(1), commercialTerms = "Terms" });
        await Ok(host.Client.PostAsync($"api/pricing/contracts/{contract}/activate", null));
        using var response = await host.Client.PostAsJsonAsync("api/pricing/resolve", new { skuId = Guid.NewGuid(), quantity = 1,
            currency = "VND", channel = 2, contractId = contract, customerId = customer });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NoApplicablePrice", (await Body(response)).GetProperty("errors").GetProperty("Code")[0].GetString());
    }

    private async Task<ProductItem> CreateDraftSku()
    {
        var product = Product.Create("Pricing test product");
        var item = ProductItem.Create(product, SKU.Create(Guid.NewGuid().ToString("N")), []);
        await using var db = fixture.Catalog();
        await new CatalogUnitOfWork(db, new PricingTestDispatcher()).ExecuteInTransactionAsync(async ct =>
        {
            await new ProductRepository(db).AddAsync(product, ct);
            await new ProductItemRepository(db).AddAsync(item, ct);
        });
        return item;
    }

    private static async Task<Guid> Create(HttpClient client, string path, object model)
    {
        using var response = await client.PostAsJsonAsync("api/pricing/" + path, model);
        var body = await Body(response);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body.ToString());
        return body.GetProperty("data").GetGuid();
    }

    private static async Task Ok(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.True(response.StatusCode == HttpStatusCode.OK, (await Body(response)).ToString());
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}