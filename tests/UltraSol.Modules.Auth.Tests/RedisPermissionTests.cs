using UltraSol.Shared.Application.Authentication;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Modules.Auth.Infrastructure.Authorization;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Shared.Application.Caching;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class RedisFactAttribute : FactAttribute
{
    public RedisFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_AUTH_POSTGRES_TESTS") != "1" || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ULTRASOL_AUTH_TEST_REDIS")))
        {
            Skip = "Enable disposable PostgreSQL tests and set ULTRASOL_AUTH_TEST_REDIS.";
        }
    }
}

public sealed class RedisPermissionTests(AuthDatabaseFixture database) : IClassFixture<AuthDatabaseFixture>
{
    [RedisFact]
    public async Task SharedRedisAdapterPersistsVersionedPermissionsAndRevocation()
    {
        var prefix = $"ultrasol-test:{Guid.NewGuid():N}:";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = Environment.GetEnvironmentVariable("ULTRASOL_AUTH_TEST_REDIS"),
            ["Redis:InstanceName"] = prefix
        }).Build();
        var registrations = new ServiceCollection();
        registrations.AddSingleton<IConfiguration>(configuration);
        var extension = typeof(PostgreSqlExtensions).Assembly.GetType("UltraSol.Shared.Infrastructure.ThirdParties.Caching.Redis.RedisExtensions")!;
        extension.GetMethod("AddRedis", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [registrations]);
        await using var redisServices = registrations.BuildServiceProvider();
        var cache = redisServices.GetRequiredService<ICacheService>();
        var redis = redisServices.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        await using var services = AuthModelTests.Services(database.Connection);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var unit = scope.ServiceProvider.GetRequiredService<IAuthUnitOfWork>();
        var user = UserAccount.Create($"{Guid.NewGuid():N}@example.com", DateTimeOffset.UtcNow);
        user.NormalizedEmail = user.Email!.ToUpperInvariant();
        user.NormalizedUserName = user.NormalizedEmail;
        var definition = new PermissionDefinition("Catalog.Products.GetProducts", "TestRequest");
        var grant = new DirectPermission(Guid.NewGuid(), user.Id, definition.Code, false);
        await unit.ExecuteInTransactionAsync(ct =>
        {
            db.AddRange(user, definition, grant);
            return Task.CompletedTask;
        });
        var permissions = new PermissionService(db, new Account(user.Id), cache, NullLogger<PermissionService>.Instance);
        var keys = new List<RedisKey> { prefix + "permission:version" };
        try
        {
            await permissions.SynchronizeAsync(default);
            var version = await cache.GetAsync<long>("permission:version");
            var key = prefix + $"permission:user:{user.Id}:v{version}";
            keys.Add(key);
            var snapshot = JsonSerializer.Deserialize<PermissionSnapshot>((string)(await redis.StringGetAsync(key))!)!;
            Assert.Equal(version, snapshot.Version);
            Assert.True(snapshot.Allows(definition.Code));
            await unit.ExecuteInTransactionAsync(ct =>
            {
                db.Remove(grant);
                return Task.CompletedTask;
            });
            await permissions.SynchronizeAsync(default);
            var nextVersion = await cache.GetAsync<long>("permission:version");
            Assert.True(nextVersion > version);
            key = prefix + $"permission:user:{user.Id}:v{nextVersion}";
            keys.Add(key);
            snapshot = JsonSerializer.Deserialize<PermissionSnapshot>((string)(await redis.StringGetAsync(key))!)!;
            Assert.False(snapshot.Allows(definition.Code));
            Assert.False((await permissions.GetAsync(user.Id, default)).Allows(definition.Code));
        }
        finally
        {
            await redis.KeyDeleteAsync(keys.ToArray());
        }
    }

    private sealed class Account(Guid userId) : ICurrentAccount
    {
        public Guid? UserId => userId;
        public Guid? SessionId => null;
    }
}