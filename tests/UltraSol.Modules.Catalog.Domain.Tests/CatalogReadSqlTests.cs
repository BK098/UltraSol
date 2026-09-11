using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Modules.Catalog.Infrastructure.Reads;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Shared.Domain.Common.Paging;
using UltraSol.Shared.Domain.Common.Exceptions;
using Xunit;

namespace UltraSol.Modules.Catalog.Domain.Tests;

public class CatalogReadSqlTests
{
    [Theory]
    [InlineData("products")]
    [InlineData("items")]
    [InlineData("brands")]
    [InlineData("categories")]
    [InlineData("collections")]
    [InlineData("variations")]
    public async Task ListsTranslateSearchAndPagingWithoutConnectingToDatabase(string kind)
    {
        var commands = new CaptureCommands();
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .AddInterceptors(new NoConnection(), commands)
            .Options);
        var reads = new CatalogReadStore(db);
        var page = PaginationRequest.Create(2, 5) with { Search = "test" };
        switch (kind)
        {
            case "products":
                await reads.ProductsAsync(page, new(), default);
                break;
            case "items":
                await reads.ItemsAsync(page, new(), default);
                break;
            case "brands":
                await reads.BrandsAsync(page, null, null, default);
                break;
            case "categories":
                await reads.CategoriesAsync(page, null, false, null, default);
                break;
            case "collections":
                await reads.CollectionsAsync(page, null, null, default);
                break;
            case "variations":
                await reads.VariationsAsync(Guid.NewGuid(), page, default);
                break;
        }
        Assert.Contains(commands.Sql, sql => sql.Contains("count(", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands.Sql, sql => sql.Contains("LIMIT") && sql.Contains("OFFSET"));
        Assert.Contains(commands.Sql, sql => sql.Contains("LIMIT") && sql.Contains("ORDER BY"));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(CollectionType.Manual, RuleMatchMode.All)]
    [InlineData(CollectionType.Automatic, RuleMatchMode.All)]
    [InlineData(CollectionType.Automatic, RuleMatchMode.Any)]
    public async Task AdminCollectionProductsFilterInSqlWithoutPublicVisibilityRestriction(CollectionType type, RuleMatchMode mode)
    {
        var commands = new CaptureCommands { CollectionType = type, MatchMode = mode };
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .AddInterceptors(new NoConnection(), commands)
            .Options);
        var reads = new CatalogReadStore(db);
        await reads.ProductsAsync(PaginationRequest.Create(1, 5), new(CollectionId: Guid.NewGuid()), default);
        var sql = Assert.Single(commands.Sql, x => x.Contains("LIMIT") && x.Contains("FROM catalog.products"));
        Assert.DoesNotContain("status = 2", sql);
        if (type == CollectionType.Manual)
        {
            Assert.Contains("collection_entries", sql);
            Assert.Contains("sort_order", sql);
        }
        else
        {
            Assert.Contains("brand_id", sql);
            Assert.Contains("product_categories", sql);
        }
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("product")]
    [InlineData("item")]
    [InlineData("bundle")]
    [InlineData("brand")]
    [InlineData("category")]
    [InlineData("collection")]
    [InlineData("variation")]
    public async Task DetailQueriesTranslateAndRejectMissingEntities(string kind)
    {
        var commands = new CaptureCommands();
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .AddInterceptors(new NoConnection(), commands)
            .Options);
        var reads = new CatalogReadStore(db);
        var id = Guid.NewGuid();
        Func<Task> action = kind switch
        {
            "product" => () => reads.ProductAsync(id, default),
            "item" => () => reads.ItemAsync(id, Guid.NewGuid(), false, default),
            "bundle" => () => reads.ItemAsync(id, Guid.NewGuid(), true, default),
            "brand" => () => reads.BrandAsync(id, default),
            "category" => () => reads.CategoryAsync(id, default),
            "collection" => () => reads.CollectionAsync(id, default),
            _ => () => reads.VariationAsync(Guid.NewGuid(), id, default)
        };
        await Assert.ThrowsAsync<EntityNotFoundException>(action);
        Assert.Contains(commands.Sql, sql => sql.Contains("WHERE"));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private sealed class NoConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private sealed class CaptureCommands : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public CollectionType? CollectionType { get; init; }
        public RuleMatchMode MatchMode { get; init; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            var table = new DataTable();
            if (CollectionType.HasValue && command.CommandText.Contains("FROM catalog.collections AS"))
            {
                table.Columns.Add("id", typeof(Guid));
                table.Columns.Add("name", typeof(string));
                table.Columns.Add("description", typeof(string));
                table.Columns.Add("type", typeof(int));
                table.Columns.Add("status", typeof(int));
                table.Columns.Add("match_mode", typeof(int));
                table.Rows.Add(Guid.NewGuid(), "Collection", "Description", (int)CollectionType.Value, 1, (int)MatchMode);
            }
            else if (CollectionType.HasValue && (command.CommandText.Contains("FROM catalog.collection_rule_brands AS") ||
                command.CommandText.Contains("FROM catalog.collection_rule_categories AS")))
            {
                table.Columns.Add("id", typeof(Guid));
                table.Columns.Add("name", typeof(string));
                table.Rows.Add(Guid.NewGuid(), "Rule reference");
            }
            else if (command.CommandText.StartsWith("SELECT count(", StringComparison.OrdinalIgnoreCase))
            {
                table.Columns.Add("count", typeof(int));
                table.Rows.Add(0);
            }
            else if (command.CommandText.StartsWith("SELECT EXISTS", StringComparison.Ordinal))
            {
                table.Columns.Add("exists", typeof(bool));
                table.Rows.Add(true);
            }
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
        }
    }
}