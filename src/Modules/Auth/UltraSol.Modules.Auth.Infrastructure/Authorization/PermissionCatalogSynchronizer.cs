using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Auth.Domain.Authorization;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Shared.Application.Services;

namespace UltraSol.Modules.Auth.Infrastructure.Authorization;

public sealed class PermissionCatalogSynchronizer(AuthDbContext db, IAuthUnitOfWork unitOfWork, PermissionCatalog catalog)
{
    public Task SynchronizeAsync(CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731010, 1)", ct);
            var discovered = catalog.Requests.ToDictionary(pair => pair.Value, pair => pair.Key.FullName!);
            var definitions = await db.Permissions.ToDictionaryAsync(value => value.Code, ct);
            foreach (var definition in definitions.Values)
            {
                definition.IsActive = discovered.ContainsKey(definition.Code);
            }
            foreach (var (code, requestType) in discovered)
            {
                var definition = new PermissionDefinition(code, requestType);
                if (definitions.TryGetValue(code, out var existing))
                {
                    db.Entry(existing).CurrentValues.SetValues(definition);
                }
                else
                {
                    db.Permissions.Add(definition);
                }
            }
        }, cancellationToken);
}