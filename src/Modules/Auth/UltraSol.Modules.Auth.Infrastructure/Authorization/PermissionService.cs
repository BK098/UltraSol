using UltraSol.Shared.Application.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using UltraSol.Modules.Auth.Application.Authorization;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Shared.Application.Caching;

namespace UltraSol.Modules.Auth.Infrastructure.Authorization;

public sealed class PermissionService(AuthDbContext db, ICurrentAccount current, ICacheService cache, ILogger<PermissionService> logger) : IPermissionService
{
    private readonly Dictionary<Guid, PermissionSnapshot> _requestCache = [];

    public async Task<PermissionSnapshot> GetAsync(Guid userId, CancellationToken ct)
    {
        EnsureCommittedRead();
        var version = await ReadVersionAsync(ct);
        if (_requestCache.TryGetValue(userId, out var snapshot) && snapshot.Version == version)
        {
            return snapshot;
        }
        snapshot = null;
        try
        {
            snapshot = await cache.GetAsync<PermissionSnapshot>(Key(userId, version), ct);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException or JsonException)
        {
            logger.LogWarning("Permission cache unavailable; reading PostgreSQL.");
        }
        if (snapshot is null || snapshot.Version != version)
        {
            snapshot = await ReadSnapshotAsync(userId, ct);
            try
            {
                await StoreAsync(userId, snapshot, ct);
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                logger.LogWarning("Permission cache write failed; PostgreSQL result remains authoritative.");
            }
        }
        _requestCache[userId] = snapshot;
        return snapshot;
    }

    public async Task SynchronizeAsync(CancellationToken ct)
    {
        EnsureCommittedRead();
        var version = await ReadVersionAsync(ct);
        if (await cache.GetAsync<long?>("permission:version", ct) == version)
        {
            return;
        }
        // ponytail: global version rebuilds every user; use per-user versions when this becomes costly.
        var userIds = await db.Users.IgnoreQueryFilters().AsNoTracking().Select(user => user.Id).ToListAsync(ct);
        foreach (var userId in userIds)
        {
            await StoreAsync(userId, await ReadSnapshotAsync(userId, ct), ct);
        }
        if (await ReadVersionAsync(ct) == version)
        {
            await cache.SetAsync("permission:version", version, TimeSpan.FromMinutes(5), ct);
        }
    }

    private Task<long> ReadVersionAsync(CancellationToken ct) =>
        db.AuthorizationVersions.AsNoTracking().Where(value => value.Id == 1).Select(value => value.Version).SingleAsync(ct);

    private void EnsureCommittedRead()
    {
        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Read permission cache outside the write transaction.");
        }
    }

    private static string Key(Guid userId, long version) => $"permission:user:{userId}:v{version}";

    private Task StoreAsync(Guid userId, PermissionSnapshot snapshot, CancellationToken ct) =>
        cache.SetAsync(Key(userId, snapshot.Version), snapshot, TimeSpan.FromMinutes(10), ct);

    private async Task<PermissionSnapshot> ReadSnapshotAsync(Guid userId, CancellationToken ct)
    {
        while (true)
        {
            var version = await ReadVersionAsync(ct);
            var activeAccount = await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.Status == AccountStatus.Active, ct)
                && !await db.Set<EmployeeAccount>().AsNoTracking().AnyAsync(employee => employee.UserId == userId && !employee.IsActive, ct);
            var roles = await (from assignment in db.RoleAssignments.AsNoTracking()
                               join role in db.Roles on assignment.RoleId equals role.Id
                               where assignment.UserId == userId && activeAccount
                               select new { role.Code, assignment.RoleId }).ToListAsync(ct);
            var roleIds = roles.Select(value => value.RoleId).ToArray();
            var roleRules = await db.RolePermissions.AsNoTracking().Where(value => roleIds.Contains(value.RoleId)).ToListAsync(ct);
            var active = await db.Permissions.AsNoTracking().Where(value => value.IsActive).Select(value => value.Code).ToListAsync(ct);
            var direct = await db.DirectPermissions.AsNoTracking().Where(value => value.UserId == userId && activeAccount).ToListAsync(ct);
            var rules = roleRules.Select(rule => new PermissionRule(rule.PermissionCode, rule.Deny))
                .Concat(direct.Select(rule => new PermissionRule(rule.PermissionCode, rule.Deny)))
                .Where(rule => active.Contains(rule.Code)).Distinct().ToArray();
            if (await ReadVersionAsync(ct) == version)
            {
                return new PermissionSnapshot(roles.Any(role => role.Code == "System"), roles.Select(role => role.Code).Distinct().ToArray(), rules) { Version = version };
            }
        }
    }

    public async Task DemandAsync(string permission, CancellationToken ct)
    {
        var userId = current.UserId ?? throw new AuthAccessException(401, "Authentication required.");
        if (!(await GetAsync(userId, ct)).Allows(permission))
        {
            throw new AuthAccessException(403, "Permission denied.");
        }
    }
}