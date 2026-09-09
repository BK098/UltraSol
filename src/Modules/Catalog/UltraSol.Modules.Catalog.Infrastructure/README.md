# Catalog Infrastructure

## Reusable module context

CatalogDbContext inherits ModuleDbContext<CatalogDbContext>. The shared base is generic over the context type; aggregate hooks currently use AggregateRoot with Guid keys. Supporting other aggregate key types is outside this refactor.

The base owns repository materialization and the asynchronous save sequence:

1. Check cancellation, transaction policy and acceptAllChangesOnSuccess.
2. Recognize newly added aggregates, check deletion policy and require complete aggregates when configured.
3. Call PrepareAggregatesAsync with the tracked aggregate roots.
4. Call EF SaveChangesAsync.

Both asynchronous save overloads use this sequence. Synchronous saves and acceptAllChangesOnSuccess=false are unsupported. Save methods are sealed so a module cannot accidentally bypass the sequence.

MaterializeAsync calls HydrateAggregateAsync and remembers successful loads of tracked aggregates by object reference. Added aggregates are already complete. Detached read results are hydrated without being marked complete for later writes. Failed hydration can be retried. Contexts retain their own materialization state; use a fresh context for a fresh unit of work, especially after rollback.

The default policies allow aggregate deletion and do not require an explicit transaction or complete aggregate materialization. Catalog explicitly sets RequireTransaction=true, AllowAggregateDeletion=false and RequireMaterializedAggregates=true. The deletion policy guards tracked aggregate deletion during SaveChangesAsync; restrictions on repository bulk/delete operations remain the module's IRepositoryWritePolicy responsibility.

## Adding another module

Create a concrete context inheriting ModuleDbContext<YourDbContext> with DbContextOptions<YourDbContext>. Keep DbSet declarations, schema and model configuration in that module. Override the policy properties as needed.

Override HydrateAggregateAsync when aggregates contain data that the shared repository must assemble after querying. Override PrepareAggregatesAsync for module-specific synchronization, validation or audit changes before EF saves. Hooks must not call SaveChangesAsync recursively, commit transactions or dispatch events. The preparation hook receives roots validated before it runs; it should prepare their data rather than introduce new aggregate roots.

A module requiring custom transaction setup can implement ITransactionPreparation. Shared UnitOfWork invokes it after opening a transaction and before the business callback. A module restricting repository delete/bulk operations can implement IRepositoryWritePolicy. Shared infrastructure has no dependency on Catalog entity types or SQL.

## Catalog responsibilities

Catalog owns hydration of product variations/media/categories, item selections/bundles and collection membership/rules. Its preparation hook synchronizes relational rows, finds owners of changed children and updates aggregate audit/concurrency values. It also clears previous primary-media flags before EF writes replacements to satisfy immediate unique indexes.

Catalog retains its transaction-scoped PostgreSQL advisory lock (731004, 1), acquired through ITransactionPreparation. Repository deletion and bulk writes remain blocked. Use aggregate Archive behavior or remove children through their owning aggregate.

Load tracked aggregates with repository methods before changing them. Raw IQueryable queries and DTO projections do not assemble ignored join data. Avoid attaching detached graphs for replacement writes.

## Registration, transactions and events

CatalogModule registers CatalogDbContext with AddPostgres<CatalogDbContext>(), discovers repositories through AddRegistration and registers ICatalogUnitOfWork with CatalogUnitOfWork. CatalogUnitOfWork inherits the shared UnitOfWork. ProductPublishedHandler is registered as a domain event handler.

Use ICatalogUnitOfWork.ExecuteInTransactionAsync for operations with business reads and writes so the Catalog lock is acquired before reads. The unit of work commits before dispatching domain events. This is in-process dispatch, not a durable outbox. Retry failed operations with a fresh scope and freshly loaded aggregates.

For externally owned transactions, IUnitOfWork.SaveChangesAsync(false) disables event dispatch. This boolean is different from DbContext.SaveChangesAsync(false), which controls accepting tracked changes and is unsupported by ModuleDbContext.

## Migration and verification status

The current workspace has removed CatalogDbContextFactory, CatalogMigrator, CatalogPostgresConfiguration and the Catalog migration files. UseCatalogModule currently creates a scope without running migrations; Catalog registration uses AddPostgres with its default runMigration=false. This refactor does not recreate or apply migrations.

Build the solution with dotnet build UltraSol.slnx. Run the Infrastructure test project explicitly; it is not currently included in that solution.

ModuleDbContextTests uses a separate sample context and an EF save interceptor to test the shared pipeline without connecting to PostgreSQL. These tests cover policy isolation, hydration, failed hydration, detached reads, added aggregates, save overloads, cancellation and Catalog's transaction requirement. They do not verify PostgreSQL persistence.

PersistenceTests includes guards for missing transactions and incomplete aggregates, plus existing aggregate round-trips and child-change concurrency checks. The full Infrastructure test project currently cannot compile because its older tests reference removed migration/startup APIs and pass EventContext to CatalogUnitOfWork, which now requires CatalogDbContext. Those pre-existing test dependencies need a separate update before PostgreSQL integration verification can run.