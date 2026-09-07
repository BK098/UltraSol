using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using UltraSol.Modules.Catalog.Api;
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
    public string Connection { get; private set; } = "";
    public async Task InitializeAsync()
    {
        using var context = new CatalogDbContextFactory().CreateDbContext([]);
        Connection = context.Database.GetConnectionString()!;
        await CatalogMigrator.MigrateAsync(context);
        await CatalogMigrator.MigrateAsync(context);
    }
    public Task DisposeAsync() => Task.CompletedTask;
    public CatalogDbContext Create() => new(new DbContextOptionsBuilder<CatalogDbContext>()
        .UseNpgsql(Connection, CatalogPostgresConfiguration.Configure).Options);
}

public class PersistenceTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static string Sku() => "TEST-" + Guid.NewGuid().ToString("N");
    private static async Task CheckConstraints(CatalogDbContext db) =>
        await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE; SET CONSTRAINTS ALL DEFERRED");
    private static Repository<T> Repo<T>(CatalogDbContext db) where T : class, IEntity<Guid> => new(db);

    [Fact]
    public async Task MigrationAndModelHaveExactly15Tables()
    {
        await using var db = fixture.Create();
        Assert.Equal(15, db.Model.GetEntityTypes().Count());
        Assert.All(db.Model.GetEntityTypes(), type => Assert.Equal("catalog", type.GetSchema()));
        Assert.DoesNotContain(db.Model.GetEntityTypes().SelectMany(x => x.GetProperties()), x => x.Name == "IsDeleted" || x.Name == "DomainEvents");
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains("20260907110000_FixReferenceTrigger", await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
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
        var bundleProduct = Product.Create("Bundle");
        var bundle = ProductItem.CreateBundle(bundleProduct, SKU.Create(Sku()), [], [(item, 2)]);
        await Repo<Brand>(db).AddAsync(brand); await Repo<Category>(db).AddAsync(category);
        await Repo<Product>(db).AddRangeAsync([p, bundleProduct]);
        await Repo<ProductItem>(db).AddRangeAsync([item, bundle]);
        await Repo<CatalogCollection>(db).AddRangeAsync([manual, automatic]);
        await new UnitOfWork(db, new DomainEventDispatcher([])).SaveChangesAsync(false);
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

    [Fact]
    public async Task LongSignatureAndDuplicateCombination()
    {
        await using var db = fixture.Create();
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = Product.Create("Many variations");
        var selections = new List<OptionSelection>();
        for (var n = 0; n < 60; n++) { var v = p.AddVariation("V" + n); selections.Add(new(v, p.AddVariationOption(v, "O"))); }
        var item = ProductItem.Create(p, SKU.Create(Sku()), selections);
        await Repo<Product>(db).AddAsync(p); await Repo<ProductItem>(db).AddAsync(item);
        await db.SaveChangesAsync(); await CheckConstraints(db);
        Assert.True(item.OptionSignature.Value.Length > 3000);
        await Repo<ProductItem>(db).AddAsync(ProductItem.Create(p, SKU.Create(Sku()), selections));
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => CheckConstraints(db));
        Assert.Equal("23505", error.SqlState);
    }

    [Fact]
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

    [Fact]
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
        p.AddMedia("https://example.com/a"); await db.SaveChangesAsync();
        stale.Rename("Stale");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
    }

    [Fact]
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

    [Fact]
    public async Task RawSqlCycleRejected()
    {
        await using var db = fixture.Create(); await using var tx = await db.Database.BeginTransactionAsync();
        var a = Category.CreateRoot("A"); var b = Category.CreateRoot("B");
        await Repo<Category>(db).AddRangeAsync([a,b]); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalog.categories SET parent_category_id = {b.Id} WHERE id = {a.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalog.categories SET parent_category_id = {a.Id} WHERE id = {b.Id}");
        Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => CheckConstraints(db))).SqlState);
    }

    [Fact]
    public async Task ModuleResolvesSharedServicesAndDoesNotMigrateInProduction()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Postgres:ConnectionString"] = fixture.Connection }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IHostEnvironment>(new EnvironmentStub());
        services.AddCatalogModule(config);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        Assert.IsType<UnitOfWork>(scope.ServiceProvider.GetRequiredKeyedService<IUnitOfWork>("catalog"));
        Assert.IsAssignableFrom<Repository<Product>>(scope.ServiceProvider.GetRequiredService<IProductRepository>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<IProductRepository>(), scope.ServiceProvider.GetRequiredService<IRepository<Product>>());
        Assert.Same(scope.ServiceProvider.GetRequiredService<IProductRepository>(), scope.ServiceProvider.GetRequiredService<ICommandRepository<Product>>());
        var app = new ApplicationBuilder(provider);
        await app.UseCatalogModuleAsync();
        await scope.ServiceProvider.GetRequiredKeyedService<IUnitOfWork>("catalog").ExecuteInTransactionAsync(async ct =>
        {
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            Assert.NotNull(context.Database.CurrentTransaction);
            await context.Brands.CountAsync(ct);
        });
    }

    [Fact]
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

    [Fact]
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
