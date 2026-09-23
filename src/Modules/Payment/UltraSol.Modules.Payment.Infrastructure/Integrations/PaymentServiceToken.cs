using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UltraSol.Modules.Payment.Application;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Payment.Infrastructure.Integrations;

public sealed class PaymentServiceToken(IHttpClientFactory clients, IConfiguration configuration, TimeProvider clock)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;
    private sealed record Login(string? AccessToken);

    public async Task<string> GetAsync(string? rejectedToken, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && _token != rejectedToken && _expiresAt > clock.GetUtcNow().AddSeconds(30))
            {
                return _token;
            }
            var client = clients.CreateClient("payment-auth");
            var email = configuration["Payment:ServiceAccount:Email"];
            var password = configuration["Payment:ServiceAccount:Password"];
            if (client.BaseAddress is null || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                throw PaymentHttp.Unavailable();
            }
            using var response = await client.PostAsJsonAsync("api/auth/token", new { email, password, mode = "token", deviceName = "Payment service" }, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw PaymentHttp.Unavailable();
            }
            var login = await response.Content.ReadFromJsonAsync<ApiResult<Login>>(cancellationToken: ct);
            var access = login is { IsSuccess: true } ? login.Data?.AccessToken : null;
            if (string.IsNullOrWhiteSpace(access))
            {
                throw PaymentHttp.Unavailable();
            }
            var parts = access.Split('.');
            if (parts.Length != 3)
            {
                throw PaymentHttp.Unavailable();
            }
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
            _expiresAt = DateTimeOffset.FromUnixTimeSeconds(json.RootElement.GetProperty("exp").GetInt64());
            if (_expiresAt <= clock.GetUtcNow())
            {
                throw PaymentHttp.Unavailable();
            }
            _token = access;
            return access;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or FormatException or KeyNotFoundException or ArgumentOutOfRangeException)
        {
            throw PaymentHttp.Unavailable();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw PaymentHttp.Unavailable();
        }
        finally
        {
            _gate.Release();
        }
    }
}