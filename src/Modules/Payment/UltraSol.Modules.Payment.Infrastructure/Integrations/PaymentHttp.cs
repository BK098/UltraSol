using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltraSol.Modules.Payment.Application;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Infrastructure.Integrations;

internal static class PaymentHttp
{
    internal static async Task<T?> SendAsync<T>(HttpClient client, HttpMethod method, string path, object? body, bool allowMissing, CancellationToken ct, string? idempotencyKey = null)
    {
        if (client.BaseAddress is null)
        {
            throw Unavailable();
        }
        using var request = new HttpRequestMessage(method, path);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (allowMissing && response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new PaymentFailure(503, "DependencyForbidden", "The Payment service account lacks a required permission.");
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized || (int)response.StatusCode >= 500)
            {
                throw Unavailable();
            }
            var result = await response.Content.ReadFromJsonAsync<ApiResult<T>>(cancellationToken: ct);
            if (result is null)
            {
                throw Unavailable();
            }
            if (!response.IsSuccessStatusCode || !result.IsSuccess)
            {
                var code = result.Errors?.GetValueOrDefault("Code")?.FirstOrDefault() ?? "UpstreamRejected";
                var status = response.IsSuccessStatusCode ? 503 : (int)response.StatusCode;
                throw new PaymentFailure(status, code, result.Message ?? "Operation rejected.");
            }
            return result.Data ?? throw Unavailable();
        }
        catch (HttpRequestException)
        {
            throw Unavailable();
        }
        catch (JsonException)
        {
            throw Unavailable();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw Unavailable();
        }
    }

    internal static PaymentFailure Unavailable() => new(503, "DependencyUnavailable", "A payment dependency is unavailable.");
}