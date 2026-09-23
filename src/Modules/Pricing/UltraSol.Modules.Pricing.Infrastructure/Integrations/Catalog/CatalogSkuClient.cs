using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltraSol.Modules.Pricing.Application.Integrations.Catalog;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Pricing.Infrastructure.Integrations.Catalog;

public sealed class CatalogSkuClient(HttpClient client, IHttpContextAccessor context) : ICatalogSkuClient
{
    public async Task<ApiResult<bool>> ExistsAsync(Guid skuId, CancellationToken cancellationToken)
    {
        if (client.BaseAddress is null)
        {
            return Unavailable();
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/catalog/skus/{skuId:D}/exists");
        var incoming = context.HttpContext?.Request;
        if (incoming?.Headers.Authorization is { Count: > 0 } authorization)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization.ToArray());
        }
        else if (incoming?.Cookies.TryGetValue(".UltraSol.Auth", out var cookie) == true)
        {
            request.Headers.TryAddWithoutValidation("Cookie", $".UltraSol.Auth={cookie}");
        }
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return ApiResultBuilder.Unauthorized<bool>("Catalog authentication is required.");
            }
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return ApiResultBuilder.Forbidden<bool>("Catalog SKU lookup permission is required.");
            }
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return Unavailable();
            }
            var result = await response.Content.ReadFromJsonAsync<ApiResult<bool?>>(cancellationToken);
            return result is { IsSuccess: true, StatusCode: 200, Data: { } exists }
                ? ApiResultBuilder.Success(exists) : Unavailable();
        }
        catch (HttpRequestException)
        {
            return Unavailable();
        }
        catch (JsonException)
        {
            return Unavailable();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable();
        }
    }

    private static ApiResult<bool> Unavailable() => ApiResultBuilder.Error<bool>("Catalog SKU lookup is unavailable.", 503,
        new Dictionary<string, string[]> { ["Code"] = ["CatalogUnavailable"] });
}