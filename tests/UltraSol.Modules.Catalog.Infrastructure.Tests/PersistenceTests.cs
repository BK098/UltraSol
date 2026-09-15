using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using UltraSol.Modules.Catalog.Api;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems.ValueObjects;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Modules.Catalog.Infrastructure.Repositories;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Domain.Common.Events;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Infrastructure.Repositories;
using Xunit;
using CatalogCollection = UltraSol.Modules.Catalog.Domain.Catalog.Collections.Collection;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace UltraSol.Modules.Catalog.Infrastructure.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly string _database = "ultrasol_catalog_test_" + Guid.NewGuid().ToString("N");
    private string? _adminConnection;
    private bool _created;
    public string Connection { get; private set; } = "";
    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_CATALOG_POSTGRES_TESTS") != "1")
        {
            return;
        }
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ULTRASOL_CATALOG_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set ULTRASOL_CATALOG_TEST_CONNECTION for disposable database tests.")) { Pooling = false };
        _adminConnection = builder.ConnectionString;
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        _created = true;
        builder.Database = _database;
        Connection = builder.ConnectionString;
        try
        {
            await using var context = Create();
            await context.Database.MigrateAsync();
            await context.Database.MigrateAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }
    public async Task DisposeAsync()
    {
        if (!_created)
        {
            return;
        }
        if (!_database.StartsWith("ultrasol_catalog_test_", StringComparison.Ordinal) || !Guid.TryParseExact(_database["ultrasol_catalog_test_".Length..], "N", out _))
        {
            throw new InvalidOperationException("Refusing to drop a database not created by this fixture.");
        }
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        _created = false;
    }
    public CatalogDbContext Create() => new(new DbContextOptionsBuilder<CatalogDbContext>()
        .UseNpgsql(Connection, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "catalog")).Options);
}

public sealed class CatalogPostgresFactAttribute : FactAttribute
{
    public CatalogPostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_CATALOG_POSTGRES_TESTS") != "1")
        {
            Skip = "Enable ULTRASOL_CATALOG_POSTGRES_TESTS for disposable PostgreSQL tests.";
        }
    }
}

public class PersistenceTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [CatalogPostgresFact]
    public async Task AttachedAggregateRequiresMaterializationInsideTransaction()
    {
        await using var db = fixture.Create();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Attach(Brand.Create("Incomplete aggregate"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await transaction.RollbackAsync();
    }

    [CatalogPostgresFact]
    public async Task SaveWithoutTransactionIsRejected()
    {
        await using var db = fixture.Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static string Sku() => "TEST-" + Guid.NewGuid().ToString("N");
    private static async Task CheckConstraints(CatalogDbContext db) =>
        await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE; SET CONSTRAINTS ALL DEFERRED");
    private static Repository<T> Repo<T>(CatalogDbContext db) where T : class, IEntity<Guid> => new(db);

    [CatalogPostgresFact]
    public async Task MigrationAndModelIncludeCatalogAndMailboxTables()
    {
        await using var db = fixture.Create();
        Assert.Equal(17, db.Model.GetEntityTypes().Count());
        Assert.All(db.Model.GetEntityTypes(), type => Assert.Equal("catalog", type.GetSchema()));
        Assert.DoesNotContain(db.Model.GetEntityTypes().SelectMany(x => x.GetProperties()), x => x.Name == "IsDeleted" || x.Name == "DomainEvents");
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [CatalogPostgresFact]
    public async Task SharedRepositoryRoundTripsFiveAggregatesAndChildren()
    {
        await using var db = fixture.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var brand = Brand.Create("Brand");
        var category = Category.CreateRoot("Category");
        var p = Product.Create("Product");
        p.AssignBrand(brand); p.AddCategory(category);
        var variation = p.AddVariation("Color");
        var option = p.AddVariationOption(variation, "Black");
        var media = p.AddMedia("https://example.com/a");
        var item = ProductItem.Create(p, SKU.Create(Sku()), [new(variation, option)]);
        item.AddMedia(p, "https://example.com/item");
        p.Publish();
        var manual = CatalogCollection.CreateManual("Manual");
        manual.AddProduct(p);
        var automatic = CatalogCollection.CreateAutomatic("Auto", [brand], [category]);
        manual.Publish();
        automatic.Publish();
        var bundleProduct = Product.Create("Bundle");
        var bundle = ProductItem.CreateBundle(bundleProduct, SKU.Create(Sku()), [], [(item, 2)]);
        await Repo<Brand>(db).AddAsync(brand); await Repo<Category>(db).AddAsync(category);
        await Repo<Product>(db).AddRangeAsync([p, bundleProduct]);
        await Repo<ProductItem>(db).AddRangeAsync([item, bundle]);
        await Repo<CatalogCollection>(db).AddRangeAsync([manual, automatic]);
        await new CatalogUnitOfWork(db, new DomainEventDispatcher([])).SaveChangesAsync(false);
        await CheckConstraints(db);
        db.ChangeTracker.Clear();
        var loaded = await Repo<Product>(db).GetRequiredByIdAsync(p.Id);
        Assert.Equal(brand.Id, loaded.BrandId); Assert.Contains(category.Id, loaded.CategoryIds);
        Assert.Single(loaded.Variations); Assert.Single(loaded.Variations[0].Options); Assert.Single(loaded.Media);
        Assert.Empty(db.ChangeTracker.Entries());
        var loadedItem = await Repo<ProductItem>(db).GetRequiredByIdAsync(item.Id);
        Assert.Equal(item.OptionSignature, loadedItem.OptionSignature);
        Assert.Single(loadedItem.OptionSelections); Assert.Single(loadedItem.Media);
        var loadedBundle = await Repo<ProductItem>(db).GetRequiredByIdAsync(bundle.Id);
        Assert.Equal(bundle.BundleDefinition, loadedBundle.BundleDefinition);
        var loadedAuto = await Repo<CatalogCollection>(db).GetRequiredByIdAsync(automatic.Id);
        Assert.Equal(automatic.Rules, loadedAuto.Rules); Assert.True(loadedAuto.Matches(loaded));
        var productQuery = new CollectionProductQuery(db);
        Assert.Contains(p.Id, await productQuery.GetProductIdsAsync(automatic.Id));
        Assert.Contains(p.Id, await productQuery.GetProductIdsAsync(manual.Id));
        Assert.Empty(await productQuery.GetProductIdsAsync(automatic.Id, p.Id));
        Assert.True((await Repo<CatalogCollection>(db).GetRequiredByIdAsync(manual.Id)).Matches(loaded));
        var editable = await Repo<Product>(db).GetTrackedRequiredAsync(p.Id);
        var stamp = editable.ConcurrencyStamp;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo<Product>(db).DeleteAsync(editable));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo<Product>(db).ExecuteDeleteAsync(x => x.Id == p.Id));
        editable.AddMedia("https://example.com/b");
        var second = editable.Media.Last().Id;
        editable.SetPrimaryMedia(second);
        await db.SaveChangesAsync();
        await CheckConstraints(db);
        Assert.NotEqual(stamp, editable.ConcurrencyStamp);
        db.ChangeTracker.Clear();
        var after = await Repo<Product>(db).GetRequiredByIdAsync(p.Id);
        Assert.Equal(second, after.Media.Single(x => x.IsPrimary).Id);
        Assert.Equal(2, after.Media.Count);
    }

    [CatalogPostgresFact]
    public async Task LongSignatureAndDuplicateCombination()
    {
        await using var db = fixture.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = Product.Create("Many variations");
        var selections = new List<OptionSelection>();
        for (var n = 0; n < 60; n++)
        {
            var v = p.AddVariation("V" + n);
            selections.Add(new(v, p.AddVariationOption(v, "O")));
        }
        var item = ProductItem.Create(p, SKU.Create(Sku()), selections);
        await Repo<Product>(db).AddAsync(p); await Repo<ProductItem>(db).AddAsync(item);
        await db.SaveChangesAsync(); await CheckConstraints(db);
        Assert.True(item.OptionSignature.Value.Length > 3000);
        await Repo<ProductItem>(db).AddAsync(ProductItem.Create(p, SKU.Create(Sku()), selections));
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => CheckConstraints(db));
        Assert.Equal("23505", error.SqlState);
    }

    [CatalogPostgresFact]
    public async Task DuplicateSkuFailsAndEventsAreRetained()
    {
        await using var db = fixture.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var first = Product.Create("A"); var second = Product.Create("B"); var sku = SKU.Create(Sku());
        await Repo<Product>(db).AddRangeAsync([first, second]);
        await Repo<ProductItem>(db).AddRangeAsync([ProductItem.Create(first, sku, []), ProductItem.Create(second, sku, [])]);
        first.AddDomainEvent(new TestEvent());
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Single(first.DomainEvents);
    }

    [CatalogPostgresFact]
    public async Task StaleAggregateCannotOverwriteChildChanges()
    {
        await using var db = fixture.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = Product.Create("Concurrency");
        await Repo<Product>(db).AddAsync(p);
        await db.SaveChangesAsync();
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(db.Database.GetDbConnection()).Options;
        await using var other = new CatalogDbContext(options);
        await other.Database.UseTransactionAsync(tx.GetDbTransaction());
        var stale = await Repo<Product>(other).GetTrackedRequiredAsync(p.Id);
        var previousStamp = p.ConcurrencyStamp;
        p.AddMedia("https://example.com/a");
        await db.SaveChangesAsync();
        Assert.NotEqual(previousStamp, p.ConcurrencyStamp);
        Assert.NotNull(p.UpdatedAt);
        stale.Rename("Stale");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
    }

    [CatalogPostgresFact]
    public async Task CollectionRuleReplacementAndManualReorderPersist()
    {
        await using var db = fixture.Create(); await using var tx = await db.Database.BeginTransactionAsync();
        var a = Product.Create("A"); var b = Product.Create("B");
        var brand1 = Brand.Create("One"); var brand2 = Brand.Create("Two");
        var manual = CatalogCollection.CreateManual("Manual"); manual.AddProduct(a); manual.AddProduct(b);
        var automatic = CatalogCollection.CreateAutomatic("Auto", [brand1], []);
        await Repo<Product>(db).AddRangeAsync([a,b]); await Repo<Brand>(db).AddRangeAsync([brand1,brand2]);
        await Repo<CatalogCollection>(db).AddRangeAsync([manual,automatic]); await db.SaveChangesAsync(); await CheckConstraints(db);
        db.ChangeTracker.Clear();
        manual = await Repo<CatalogCollection>(db).GetTrackedRequiredAsync(manual.Id);
        automatic = await Repo<CatalogCollection>(db).GetTrackedRequiredAsync(automatic.Id);
        manual.ReorderProducts([b.Id,a.Id]); automatic.ReplaceRules([brand2], [], RuleMatchMode.Any);
        await db.SaveChangesAsync(); await CheckConstraints(db); db.ChangeTracker.Clear();
        Assert.Equal(b.Id, (await Repo<CatalogCollection>(db).GetRequiredByIdAsync(manual.Id)).Entries[0].ProductId);
        Assert.Equal(brand2.Id, (await Repo<CatalogCollection>(db).GetRequiredByIdAsync(automatic.Id)).Rules!.BrandIds[0]);
    }

    [CatalogPostgresFact]
    public async Task RawSqlCycleRejected()
    {
        await using var db = fixture.Create(); await using var tx = await db.Database.BeginTransactionAsync();
        var a = Category.CreateRoot("A"); var b = Category.CreateRoot("B");
        await Repo<Category>(db).AddRangeAsync([a,b]); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalog.categories SET parent_category_id = {b.Id} WHERE id = {a.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalog.categories SET parent_category_id = {a.Id} WHERE id = {b.Id}");
        Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => CheckConstraints(db))).SqlState);
    }

    [CatalogPostgresFact]
    public async Task ConcurrentRawSqlCannotCommitCategoryCycle()
    {
        await using var db = fixture.Create();
        var a = Category.CreateRoot("Concurrent A");
        var b = Category.CreateRoot("Concurrent B");
        await using (var setup = await db.Database.BeginTransactionAsync())
        {
            await Repo<Category>(db).AddRangeAsync([a, b]);
            await db.SaveChangesAsync();
            await setup.CommitAsync();
        }
        await using var first = new NpgsqlConnection(fixture.Connection);
        await using var second = new NpgsqlConnection(fixture.Connection);
        await first.OpenAsync();
        await second.OpenAsync();
        await using var firstTransaction = await first.BeginTransactionAsync();
        await using var secondTransaction = await second.BeginTransactionAsync();
        await using var firstUpdate = new NpgsqlCommand("UPDATE catalog.categories SET parent_category_id = @parent WHERE id = @id", first, firstTransaction);
        firstUpdate.Parameters.AddWithValue("parent", b.Id);
        firstUpdate.Parameters.AddWithValue("id", a.Id);
        await firstUpdate.ExecuteNonQueryAsync();
        await using var secondUpdate = new NpgsqlCommand("UPDATE catalog.categories SET parent_category_id = @parent WHERE id = @id", second, secondTransaction);
        secondUpdate.Parameters.AddWithValue("parent", a.Id);
        secondUpdate.Parameters.AddWithValue("id", b.Id);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pending = secondUpdate.ExecuteNonQueryAsync(timeout.Token);
        await firstTransaction.CommitAsync(timeout.Token);
        await pending;
        var error = await Assert.ThrowsAsync<PostgresException>(() => secondTransaction.CommitAsync(timeout.Token));
        Assert.Equal("23514", error.SqlState);
    }

    [CatalogPostgresFact]
    public async Task IntegrityWritesRejectStaleSnapshotIsolation()
    {
        await using var connection = new NpgsqlConnection(fixture.Connection);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        await using var command = new NpgsqlCommand("UPDATE catalog.categories SET parent_category_id = NULL WHERE false", connection, transaction);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("0A000", error.SqlState);
    }

    [CatalogPostgresFact]
    public async Task ModuleResolvesSharedServicesAndDoesNotMigrateInProduction()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Postgres:ConnectionString"] = fixture.Connection }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IHostEnvironment>(new EnvironmentStub());
        services.AddSingleton<IConfiguration>(config);
        services.AddScoped<IDomainEventDispatcher>(_ => new DomainEventDispatcher([]));
        services.AddCatalogModule(config);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<CatalogUnitOfWork>(scope.ServiceProvider.GetRequiredService<ICatalogUnitOfWork>());
        Assert.IsAssignableFrom<Repository<Product>>(scope.ServiceProvider.GetRequiredService<IProductRepository>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<IProductRepository>(), scope.ServiceProvider.GetRequiredService<IProductRepository>());
        Assert.IsAssignableFrom<IRepository<Product>>(scope.ServiceProvider.GetRequiredService<IRepository<Product>>());
        var app = new ApplicationBuilder(provider);
        app.UseCatalogModule();
        await scope.ServiceProvider.GetRequiredService<ICatalogUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            Assert.NotNull(context.Database.CurrentTransaction);
            await context.Brands.CountAsync(ct);
        });
    }

    [CatalogPostgresFact]
    public async Task CatalogLockSerializesConcurrentWritersWithoutLeavingTestRows()
    {
        await using var first = fixture.Create(); await using var second = fixture.Create();
        await using var tx1 = await first.Database.BeginTransactionAsync();
        await first.PrepareTransactionAsync();
        await using var tx2 = await second.Database.BeginTransactionAsync();
        await second.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '150ms'");
        var error = await Assert.ThrowsAsync<PostgresException>(() => second.PrepareTransactionAsync());
        Assert.Equal("55P03", error.SqlState);
        await tx2.RollbackAsync();
        await tx1.RollbackAsync();
        await using var tx3 = await second.Database.BeginTransactionAsync();
        await second.PrepareTransactionAsync();
    }

    [CatalogPostgresFact]
    public async Task ForeignProductOptionIsRejectedByDatabase()
    {
        await using var db = fixture.Create(); await using var tx = await db.Database.BeginTransactionAsync();
        var a = Product.Create("A"); var b = Product.Create("B");
        var av = a.AddVariation("Color"); var ao = a.AddVariationOption(av, "Black");
        var bv = b.AddVariation("Color"); var bo = b.AddVariationOption(bv, "White");
        var item = ProductItem.Create(a, SKU.Create(Sku()), [new(av, ao)]);
        await Repo<Product>(db).AddRangeAsync([a,b]); await Repo<ProductItem>(db).AddAsync(item);
        await db.SaveChangesAsync(); await CheckConstraints(db);
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalog.product_item_selections SET option_id = {bo} WHERE product_item_id = {item.Id}"));
        Assert.Equal("23503", error.SqlState);
    }

    private sealed class EnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed record TestEvent : DomainEvent;
}