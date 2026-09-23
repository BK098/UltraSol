using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Shared.Application.Caching;
using UltraSol.Shared.Infrastructure.Api;
using UltraSol.Shared.Infrastructure.Exceptions;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class AuthHttpTests(AuthDatabaseFixture database) : IClassFixture<AuthDatabaseFixture>
{
    [PostgresFact]
    public async Task Anonymous_login_resolves_real_JWT_and_cookie_accounts_and_revoked_sessions_lose_self_service_access()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Postgres:ConnectionString"] = database.Connection,
            ["Auth:SigningKey"] = "auth-http-test-signing-key-only-at-least-32-bytes"
        });
        var module = Assembly.Load("UltraSol.Modules.Auth.Api").GetType("UltraSol.Modules.Auth.Api.AuthModule")!;
        module.GetMethod("AddAuthModule")!.Invoke(null, [builder.Services, builder.Configuration]);
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddSingleton<ICacheService, UncachedPermissions>();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        var internalControllers = typeof(BaseController).Assembly.GetType("UltraSol.Shared.Infrastructure.Api.InternalControllerFeatureProvider")!;
        builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
            manager.FeatureProviders.Add((IApplicationFeatureProvider<ControllerFeature>)Activator.CreateInstance(internalControllers)!));
        await using var app = builder.Build();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            await scope.ServiceProvider.GetRequiredService<IAuthUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                db.Roles.Add(new Role("Customer", "Customer"));
                return Task.CompletedTask;
            });
        }
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        var credentials = new { email = $"{Guid.NewGuid():N}@example.com", password = "Auth-Http-482!" };
        var registered = await Data(await client.PostAsJsonAsync("api/auth/register", credentials));
        var userId = registered.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("api/auth/token", new { credentials.email, password = "wrong" })).StatusCode);
        var login = await Data(await client.PostAsJsonAsync("api/auth/token", credentials));
        Assert.Equal(userId, login.GetProperty("userId").GetGuid());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        Assert.Equal(userId, (await Data(await client.GetAsync("api/auth/me"))).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/auth/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("api/auth/accounts")).StatusCode);
        await Data(await client.PostAsync("api/auth/logout", null));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("api/auth/login", credentials)).StatusCode);
        using var csrfResponse = await client.GetAsync("api/auth/csrf");
        var csrf = (await csrfResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        var csrfCookie = csrfResponse.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", csrfCookie);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        using var cookieResponse = await client.PostAsJsonAsync("api/auth/login", credentials);
        var cookieLogin = await Data(cookieResponse);
        Assert.Equal(userId, cookieLogin.GetProperty("userId").GetGuid());
        var authCookie = cookieResponse.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(".UltraSol.Auth=", StringComparison.Ordinal)).Split(';')[0];
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", csrfCookie + "; " + authCookie);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(userId, (await Data(await client.GetAsync("api/auth/me"))).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("api/auth/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("api/auth/logout", null)).StatusCode);
        using var authenticatedCsrf = await client.GetAsync("api/auth/csrf");
        csrf = (await authenticatedCsrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
        await Data(await client.PostAsync("api/auth/logout", null));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/auth/me")).StatusCode);
        await app.StopAsync();
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body).GetProperty("data");
    }

    private sealed class UncachedPermissions : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}