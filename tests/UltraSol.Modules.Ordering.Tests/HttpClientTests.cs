using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Infrastructure.Integrations;
using UltraSol.Shared.Application.Responses;
using Xunit;

namespace UltraSol.Modules.Ordering.Tests;

public sealed class HttpClientTests
{
    [Fact]
    public async Task Forbidden_does_not_refresh_token_or_repeat_the_HTTP_request()
    {
        var clock = new OrderingTestClock();
        var logins = 0;
        using var auth = new HttpClient(new CallbackHandler(_ =>
        {
            logins++;
            return Task.FromResult(Json(ApiResultBuilder.Success(new { accessToken = Jwt(clock.Now.AddMinutes(5)) })));
        })) { BaseAddress = new Uri("http://auth.test") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ordering:ServiceAccount:Email"] = "ordering@example.test", ["Ordering:ServiceAccount:Password"] = "test-only"
        }).Build();
        var calls = 0;
        using var handler = new OrderingServiceAuthHandler(new(new ClientFactory(auth), config, clock))
        {
            InnerHandler = new CallbackHandler(_ =>
            {
                calls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            })
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://catalog.test") };
        var failure = await Assert.ThrowsAsync<OrderingFailure>(() => new CatalogCheckoutClient(client).GetAsync([Guid.NewGuid()], default));
        Assert.Equal("DependencyForbidden", failure.Code);
        Assert.Equal((1, 1), (logins, calls));
    }

    [Fact]
    public async Task Service_token_is_single_flight_and_401_refreshes_once_with_identical_body()
    {
        var clock = new OrderingTestClock();
        var logins = 0;
        var loginHandler = new CallbackHandler(request =>
        {
            var sequence = Interlocked.Increment(ref logins);
            return Task.FromResult(Json(ApiResultBuilder.Success(new { accessToken = Jwt(clock.Now.AddMinutes(5), sequence) })));
        });
        using var authClient = new HttpClient(loginHandler) { BaseAddress = new Uri("http://auth.test") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Ordering:ServiceAccount:Email"] = "ordering@example.test", ["Ordering:ServiceAccount:Password"] = "test-only"
        }).Build();
        var tokens = new OrderingServiceToken(new ClientFactory(authClient), config, clock);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => tokens.GetAsync(null, default)));
        Assert.Equal(1, logins);
        var bodies = new List<string>();
        var bearers = new List<string>();
        var calls = 0;
        using var handler = new OrderingServiceAuthHandler(tokens)
        {
            InnerHandler = new CallbackHandler(async request =>
            {
                bodies.Add(await request.Content!.ReadAsStringAsync());
                bearers.Add(request.Headers.Authorization!.Parameter!);
                Assert.False(request.Headers.Contains("Cookie"));
                return ++calls == 1 ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : Json(ApiResultBuilder.Success(new CatalogItems([])));
            })
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://catalog.test") };
        await new CatalogCheckoutClient(http).GetAsync([Guid.NewGuid()], default);
        Assert.Equal(2, logins);
        Assert.Equal(2, calls);
        Assert.Equal(bodies[0], bodies[1]);
        Assert.NotEqual(bearers[0], bearers[1]);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(503)]
    public async Task Failed_dependencies_never_become_successful_reservations(int status)
    {
        using var client = new HttpClient(new CallbackHandler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))))
        {
            BaseAddress = new Uri("http://inventory.test")
        };
        var failure = await Assert.ThrowsAsync<OrderingFailure>(() => new InventoryReservationClient(client).FindAsync(Guid.NewGuid(), default));
        Assert.Equal(503, failure.Status);
    }

    [Fact]
    public async Task Timeout_invalid_json_and_missing_data_are_dependency_failures()
    {
        foreach (var handler in new[]
        {
            new CallbackHandler(_ => throw new TaskCanceledException("timeout")),
            new CallbackHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid json") })),
            new CallbackHandler(_ => Task.FromResult(Json(new { isSuccess = true, data = (object?)null, statusCode = 200 })))
        })
        {
            using var client = new HttpClient(handler) { BaseAddress = new Uri("http://catalog.test") };
            Assert.Equal(503, (await Assert.ThrowsAsync<OrderingFailure>(() => new CatalogCheckoutClient(client).GetAsync([Guid.NewGuid()], default))).Status);
        }
    }

    internal static string Jwt(DateTimeOffset expiresAt, int sequence = 1) => "test." + Convert.ToBase64String(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { exp = expiresAt.ToUnixTimeSeconds(), sequence }))).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".signature";
    private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    private sealed class CallbackHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => callback(request);
    }
    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}