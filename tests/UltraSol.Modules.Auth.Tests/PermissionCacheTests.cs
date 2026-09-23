using UltraSol.Shared.Application.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Modules.Auth.Infrastructure.Authorization;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Modules.Catalog.Application.Features.Products.Commands;
using UltraSol.Modules.Catalog.Application.Features.Products.Queries;
using UltraSol.Shared.Application.Caching;
using UltraSol.Shared.Application.Services;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class PermissionCacheTests(AuthDatabaseFixture database) : IClassFixture<AuthDatabaseFixture>
{
    [PostgresFact]
    public async Task DiscoveryAddsCommandsAndQueriesOnceWithoutGrantingPermissions()
    {
        await using var services = AuthModelTests.Services(database.Connection);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var unit = scope.ServiceProvider.GetRequiredService<IAuthUnitOfWork>();
        var catalog = new PermissionCatalog([typeof(CreateProductCommand).Assembly]);
        var synchronizer = new PermissionCatalogSynchronizer(db, unit, catalog);
        var grants = await db.RolePermissions.CountAsync();
        await synchronizer.SynchronizeAsync(default);
        Assert.True(await db.Permissions.AnyAsync(value => value.Code == catalog.Requests[typeof(CreateProductCommand)] && value.IsActive));
        Assert.True(await db.Permissions.AnyAsync(value => value.Code == catalog.Requests[typeof(GetProductsQuery)] && value.IsActive));
        Assert.Equal(catalog.Requests.Count, await db.Permissions.CountAsync(value => value.IsActive));
        var version = await db.AuthorizationVersions.Select(value => value.Version).SingleAsync();
        await synchronizer.SynchronizeAsync(default);
        Assert.Equal(version, await db.AuthorizationVersions.Select(value => value.Version).SingleAsync());
        Assert.Equal(grants, await db.RolePermissions.CountAsync());

        await new PermissionCatalogSynchronizer(db, unit, new PermissionCatalog([])).SynchronizeAsync(default);
        Assert.False(await db.Permissions.AnyAsync(value => value.IsActive));
        await synchronizer.SynchronizeAsync(default);
        Assert.Equal(catalog.Requests.Count, await db.Permissions.CountAsync(value => value.IsActive));
    }

    [PostgresFact]
    public async Task ChangesRefreshRedisWithoutAUserRequestAndRollbackDoesNotPublish()
    {
        await using var services = AuthModelTests.Services(database.Connection);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var unit = scope.ServiceProvider.GetRequiredService<IAuthUnitOfWork>();
        var cache = new RecordingCache();
        var user = UserAccount.Create($"{Guid.NewGuid():N}@example.com", DateTimeOffset.UtcNow);
        user.NormalizedEmail = user.Email!.ToUpperInvariant();
        user.NormalizedUserName = user.NormalizedEmail;
        var permission = new PermissionDefinition($"test.{Guid.NewGuid():N}", "TestRequest");
        var role = new Role(Guid.NewGuid().ToString("N"), "Test");
        var assignment = new RoleAssignment(Guid.NewGuid(), user.Id, role.Id);
        var rolePermission = new RolePermission(role.Id, permission.Code, false);
        await unit.ExecuteInTransactionAsync(ct =>
        {
            db.AddRange(user, permission, role, assignment, rolePermission);
            return Task.CompletedTask;
        });
        var permissions = new PermissionService(db, new Account(user.Id), cache, NullLogger<PermissionService>.Instance);
        await permissions.SynchronizeAsync(default);
        var version = await db.AuthorizationVersions.Select(value => value.Version).SingleAsync();
        var key = $"permission:user:{user.Id}:v{version}";
        Assert.True((await cache.GetAsync<PermissionSnapshot>(key))!.Allows(permission.Code));
        Assert.Equal(version, (await permissions.GetAsync(user.Id, default)).Version);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteInTransactionAsync(async ct =>
        {
            db.Remove(rolePermission);
            await db.SaveChangesAsync(ct);
            await Assert.ThrowsAsync<InvalidOperationException>(() => permissions.GetAsync(user.Id, ct));
            throw new InvalidOperationException("Rollback");
        }));
        Assert.Equal(version, await db.AuthorizationVersions.Select(value => value.Version).SingleAsync());
        Assert.True((await permissions.GetAsync(user.Id, default)).Allows(permission.Code));

        await unit.ExecuteInTransactionAsync(async ct =>
        {
            db.Remove(await db.RolePermissions.SingleAsync(value => value.RoleId == role.Id, ct));
        });
        Assert.False((await permissions.GetAsync(user.Id, default)).Allows(permission.Code));
        await permissions.SynchronizeAsync(default);
        var revokedVersion = await cache.GetAsync<long>("permission:version");
        Assert.True(revokedVersion > version);
        Assert.False((await cache.GetAsync<PermissionSnapshot>($"permission:user:{user.Id}:v{revokedVersion}"))!.Allows(permission.Code));

        await unit.ExecuteInTransactionAsync(ct =>
        {
            db.DirectPermissions.Add(new DirectPermission(Guid.NewGuid(), user.Id, permission.Code, false));
            return Task.CompletedTask;
        });
        cache.FailWrites = true;
        await Assert.ThrowsAsync<TimeoutException>(() => permissions.SynchronizeAsync(default));
        Assert.Equal(revokedVersion, await cache.GetAsync<long>("permission:version"));
        cache.FailWrites = false;
        await permissions.SynchronizeAsync(default);
        Assert.True((await permissions.GetAsync(user.Id, default)).Allows(permission.Code));

        await unit.ExecuteInTransactionAsync(async ct =>
        {
            (await db.Permissions.SingleAsync(value => value.Code == permission.Code, ct)).IsActive = false;
        });
        await permissions.SynchronizeAsync(default);
        Assert.False((await permissions.GetAsync(user.Id, default)).Allows(permission.Code));
    }

    [PostgresFact]
    public async Task SuspendedSystemHasNoPermissions()
    {
        await using var services = AuthModelTests.Services(database.Connection);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var unit = scope.ServiceProvider.GetRequiredService<IAuthUnitOfWork>();
        var cache = new RecordingCache();
        var user = UserAccount.Create($"{Guid.NewGuid():N}@example.com", DateTimeOffset.UtcNow);
        user.NormalizedEmail = user.Email!.ToUpperInvariant();
        user.NormalizedUserName = user.NormalizedEmail;
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            var role = await db.Roles.SingleOrDefaultAsync(value => value.Code == "System", ct);
            if (role is null)
            {
                role = new Role("System", "System");
                db.Roles.Add(role);
            }
            db.Users.Add(user);
            db.RoleAssignments.Add(new RoleAssignment(Guid.NewGuid(), user.Id, role.Id));
        });
        var permissions = new PermissionService(db, new Account(user.Id), cache, NullLogger<PermissionService>.Instance);
        Assert.True((await permissions.GetAsync(user.Id, default)).IsSystem);
        await unit.ExecuteInTransactionAsync(ct =>
        {
            user.Suspend(DateTimeOffset.UtcNow);
            return Task.CompletedTask;
        });
        await permissions.SynchronizeAsync(default);
        var snapshot = await permissions.GetAsync(user.Id, default);
        Assert.False(snapshot.IsSystem);
        Assert.Empty(snapshot.Rules);
        Assert.Empty(snapshot.Roles);
    }

    private sealed class Account(Guid userId) : ICurrentAccount
    {
        public Guid? UserId => userId;
        public Guid? SessionId => null;
    }

    private sealed class RecordingCache : ICacheService
    {
        private readonly Dictionary<string, string> _values = [];
        public bool FailWrites { get; set; }
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var value) ? JsonSerializer.Deserialize<T>(value) : default);
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            if (FailWrites)
            {
                throw new TimeoutException();
            }
            _values[key] = JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(_values.ContainsKey(key));
    }
}