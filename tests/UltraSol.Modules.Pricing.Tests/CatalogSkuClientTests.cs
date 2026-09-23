using System.Net;
using Microsoft.AspNetCore.Http;
using UltraSol.Modules.Pricing.Infrastructure.Integrations.Catalog;
using Xunit;

namespace UltraSol.Modules.Pricing.Tests;

public sealed class CatalogSkuClientTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Lookup_forwards_only_authentication_and_preserves_existence(bool exists)
    {
        var skuId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer test-token";
        context.Request.Headers.Cookie = "unrelated=private; .UltraSol.Auth=cookie-token";
        using var http = Client((request, _) =>
        {
            Assert.Equal($"https://catalog.test/api/catalog/skus/{skuId}/exists", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer test-token", request.Headers.Authorization!.ToString());
            Assert.False(request.Headers.Contains("Cookie"));
            return Task.FromResult(Response(200, $$"""{"isSuccess":true,"statusCode":200,"data":{{exists.ToString().ToLowerInvariant()}}}"""));
        });
        var result = await new CatalogSkuClient(http, new HttpContextAccessor { HttpContext = context }).ExistsAsync(skuId, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(exists, result.Data);
    }

    [Fact]
    public async Task Cookie_authentication_forwards_only_the_auth_cookie()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "unrelated=private; .UltraSol.Auth=cookie-token";
        using var http = Client((request, _) =>
        {
            Assert.Equal(".UltraSol.Auth=cookie-token", Assert.Single(request.Headers.GetValues("Cookie")));
            return Task.FromResult(Response(200, """{"isSuccess":true,"statusCode":200,"data":true}"""));
        });
        Assert.True((await new CatalogSkuClient(http, new HttpContextAccessor { HttpContext = context }).ExistsAsync(Guid.NewGuid(), default)).Data);
    }

    [Theory]
    [InlineData(401, "", 401)]
    [InlineData(403, "", 403)]
    [InlineData(404, "", 503)]
    [InlineData(500, "", 503)]
    [InlineData(200, "invalid", 503)]
    [InlineData(200, "null", 503)]
    [InlineData(200, "{\"isSuccess\":true,\"statusCode\":200}", 503)]
    [InlineData(200, "{\"isSuccess\":false,\"statusCode\":200,\"data\":false}", 503)]
    public async Task Unusable_response_is_never_reported_as_missing_sku(int status, string body, int expected)
    {
        using var http = Client((_, _) => Task.FromResult(Response(status, body)));
        var result = await new CatalogSkuClient(http, new HttpContextAccessor()).ExistsAsync(Guid.NewGuid(), default);
        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.StatusCode);
        if (expected == 503)
        {
            Assert.Equal("CatalogUnavailable", Assert.Single(result.Errors!["Code"]));
        }
    }

    [Fact]
    public async Task Transport_failure_timeout_and_missing_configuration_are_unavailable_but_caller_cancellation_propagates()
    {
        foreach (var exception in new Exception[] { new HttpRequestException(), new TaskCanceledException() })
        {
            using var http = Client((_, _) => Task.FromException<HttpResponseMessage>(exception));
            var result = await new CatalogSkuClient(http, new HttpContextAccessor()).ExistsAsync(Guid.NewGuid(), default);
            Assert.Equal(503, result.StatusCode);
        }
        using var missing = new HttpClient();
        Assert.Equal(503, (await new CatalogSkuClient(missing, new HttpContextAccessor()).ExistsAsync(Guid.NewGuid(), default)).StatusCode);
        using var cancelled = Client((_, token) => Task.FromCanceled<HttpResponseMessage>(token));
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CatalogSkuClient(cancelled, new HttpContextAccessor()).ExistsAsync(Guid.NewGuid(), source.Token));
    }

    private static HttpResponseMessage Response(int status, string body) => new((HttpStatusCode)status) { Content = new StringContent(body) };
    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => new(new Handler(send)) { BaseAddress = new Uri("https://catalog.test/") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}