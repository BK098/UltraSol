# Catalog Infrastructure

## Dependencies and module registration

Bootstrapper references Catalog.Api; Catalog.Api references Catalog.Infrastructure.
Catalog.Infrastructure references Catalog.Domain and UltraSol.Shared.Infrastructure.
Runtime EF/Npgsql packages flow from Shared. EF Core Design is a direct private tooling dependency.
Each module owns Persistence/Schema.cs (catalog and auth).

All Catalog registration is in CatalogModule.AddCatalogModule. Repositories inherit the existing
Shared Repository<T>; IRepository<T>, IQueryRepository<T>, ICommandRepository<T> and the specific
Catalog repository interface resolve to the same scoped instance.

UnitOfWork is the Shared implementation, registered under the key "catalog". There is no separate
CatalogTransactionExecutor or CatalogUnitOfWork. Keyed registration avoids selecting the wrong
DbContext when other modules add their own UnitOfWork.

## Example write

```csharp
public sealed class RenameProduct(
    IProductRepository products,
    [FromKeyedServices("catalog")] IUnitOfWork unitOfWork)
{
    public Task Execute(Guid id, string name, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var product = await products.GetTrackedRequiredAsync(id, ct);
            product.Rename(name);
            // Shared UnitOfWork saves and commits after the callback.
        }, cancellationToken);
}
```

Use FindTrackedAsync/GetTrackedRequiredAsync inside the transaction for mutations.
GetByIdAsync/ListAsync/entity paging are read-only and load a complete aggregate without tracking.
Do not attach a detached aggregate for graph replacement through UpdateAsync; read the tracked
aggregate and call its behavior instead. This preserves originals, removed children and concurrency.
Raw IQueryable Query(...) and DTO projections remain low-level query APIs and do not assemble
ignored join data. Use materializing repository methods to obtain complete aggregates.

Shared IRepositoryMaterializer is an optional context hook: contexts without it keep their existing
behavior. Catalog uses it for relational category links, item selections/bundles and collection rules.
New aggregates added through Shared AddAsync/AddRangeAsync are recognized by CatalogDbContext.
Saving modified children refreshes the owning aggregate's audit/concurrency token.

Catalog blocks repository Delete/ExecuteDelete/bulk update because they bypass Archive and aggregate
rules. Remove children through their AR. The SQL account remains privileged; direct SQL is an
administrative path, not an alternative domain write API.

## Transactions and events

Shared ITransactionPreparation allows Catalog to acquire a transaction-scoped advisory lock before
business reads. The initial implementation serializes Catalog writers using one lock (731004, 1).
This is deliberately conservative: it protects hierarchy and archive/reference races, at the cost
of concurrent write throughput. Reads do not acquire this lock.

Use ExecuteInTransactionAsync with provider retry enabled; it enters an EF execution-strategy scope.
It executes the write callback once. It does not replay captured repositories or mutated entities
on the same DbContext. Retry a transient failure at the caller boundary with a fresh DI scope and
fresh database reads. Read retry configuration in Shared AddPostgres is retained.

BeginTransactionAsync is available for manual ownership; with a retry-enabled provider the caller
must place the entire manual transaction inside a suitable execution-strategy scope. Do not begin
nested transactions. SaveChangesAsync alone opens/commits its own transaction, but a write involving
prior reads must use ExecuteInTransactionAsync so locks are acquired before those reads.

Events remain on the owner if saving/commit fails. Dispatch occurs after commit. A handler failure
must not cause the business transaction to be replayed; its database changes have already committed.
Events are removed only after successful dispatch. This is in-process delivery, not a durable outbox:
process failure between commit and dispatch still needs an outbox when reliable integration events
become a requirement. DomainEventDispatcher invokes registered IDomainEventHandler implementations;
there are currently no Catalog event handlers.

For externally-owned test transactions, call SaveChangesAsync(false) to disable dispatch.
IUnitOfWork-owned transactions coordinate post-commit dispatch.

## Migration and PostgreSQL

CatalogPostgresConfiguration shares the migrations assembly and catalog.__EFMigrationsHistory
between runtime and CatalogDbContextFactory. It does not install PostgreSQL or create a connection
independently of Postgres:ConnectionString.

UseCatalogModuleAsync runs migrations after Build only in Development. It creates the configured
database if missing, checks for unmanaged Catalog tables and never drops/resets the database.
The configured development database is ultrasol_database. Other environments use explicit tooling.

Factory reads Bootstrapper appsettings.json, appsettings.{environment}.json, development user-secrets,
then environment variables. Postgres__ConnectionString overrides the connection without changing files.
Passwords are not logged. The factory's user-secret ID matches Bootstrapper.

```powershell
dotnet ef migrations add MigrationName --project src/Modules/Catalog/UltraSol.Modules.Catalog.Infrastructure --startup-project src/Modules/Catalog/UltraSol.Modules.Catalog.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/Modules/Catalog/UltraSol.Modules.Catalog.Infrastructure --startup-project src/Modules/Catalog/UltraSol.Modules.Catalog.Infrastructure
dotnet ef migrations has-pending-model-changes --project src/Modules/Catalog/UltraSol.Modules.Catalog.Infrastructure --startup-project src/Modules/Catalog/UltraSol.Modules.Catalog.Infrastructure
```

Use a dotnet-ef 10.x tool matching the EF runtime when updating local tooling.
Migration 1 creates 15 tables; migration 2 installs deferred integrity triggers; migration 3 fixes
reference-trigger record access across different tables. Existing migration history is preserved.

SKU is unique. Option signature has no length-based unique index and supports more than 16 variations:
the deferred check compares full signatures under the Catalog write lock. Selection composite FKs
enforce Product/variation/option ownership. Deferred triggers also protect bundle composition,
collection type/rules and category cycles. No soft-delete columns are mapped.

## Verification

```powershell
dotnet build UltraSol.slnx
dotnet test UltraSol.slnx
```

Infrastructure tests use the configured PostgreSQL database. Migration is persistent; test row changes
are rolled back. Event tests commit empty transactions only. Tests cover complete aggregate round-trips,
primary media switching, long signatures, duplicate SKU/combination, stale updates, reference FKs,
collection matching, writer serialization, DI and event timing.

