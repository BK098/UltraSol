using System.Reflection;
using UltraSol.Modules.Catalog.Infrastructure.Reads;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using UltraSol.Modules.Catalog.Domain.Catalog.Collections;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Modules.Catalog.Infrastructure.Persistence.Migrations;
using UltraSol.Shared.Infrastructure.Api;
using Xunit;

namespace UltraSol.Modules.Catalog.Domain.Tests;

public class CollectionInfrastructureTests
{
    [Fact]
    public void MigrationPreservesVisibilityAndModelMatchesSnapshot()
    {
        using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);
        var migrations = db.GetService<IMigrationsAssembly>();
        var migration = migrations.CreateMigration(migrations.Migrations["20260910110000_AddCollectionStatus"], db.Database.ProviderName!);
        var operations = migration.UpOperations;
        Assert.IsType<AddColumnOperation>(operations[0]);
        Assert.Contains("CASE WHEN is_archived THEN 4 ELSE 2 END", Assert.IsType<SqlOperation>(operations[1]).Sql);
        Assert.Equal("is_archived", Assert.IsType<DropColumnOperation>(operations[2]).Name);
        Assert.Contains("status = 4", Assert.IsType<SqlOperation>(migration.DownOperations[1]).Sql);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.NotNull(migration.TargetModel.FindEntityType(typeof(Collection).FullName!)!.FindProperty("Status"));
        Assert.Null(migration.TargetModel.FindEntityType(typeof(Collection).FullName!)!.FindProperty("IsArchived"));
        var script = db.GetService<IMigrator>().GenerateScript("20260910100000_AddBrandStateAndLogo", "20260910110000_AddCollectionStatus");
        Assert.Contains("UPDATE catalog.collections", script);
    }

    [Fact]
    public void SwaggerDiscoversAllCollectionEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        var services = builder.Services;
        services.AddLogging();
        services.AddCatalogReads();
        var apiAssembly = Assembly.Load("UltraSol.Modules.Catalog.Api");
        var providerType = typeof(BaseController).Assembly.GetType("UltraSol.Shared.Infrastructure.Api.InternalControllerFeatureProvider")!;
        services.AddControllers()
            .AddApplicationPart(apiAssembly)
            .ConfigureApplicationPartManager(manager =>
            {
                manager.FeatureProviders.Add((IApplicationFeatureProvider<ControllerFeature>)Activator.CreateInstance(providerType)!);
            });
        services.AddSwaggerGen(options => options.SwaggerDoc("v1", new() { Title = "UltraSol API", Version = "v1" }));
        using var provider = services.BuildServiceProvider();
        var document = provider.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        Assert.Equal(29, document.Paths.Values.Sum(path => path.Operations!.Keys.Count(method =>
            string.Equals(method.ToString(), "GET", StringComparison.OrdinalIgnoreCase))));
        Assert.Contains("GetProductsQueryResponse", document.Components!.Schemas!.Keys);
        Assert.Contains("GetProductDetailQueryResponse", document.Components.Schemas.Keys);
        var paths = document.Paths.Where(x => x.Key.StartsWith("/api/collections", StringComparison.Ordinal)).ToArray();
        Assert.Equal(9, paths.Length);
        Assert.Equal(11, paths.Sum(x => x.Value.Operations!.Count));
        Assert.Contains(paths, x => x.Key == "/api/collections/{collectionId}/products/order");
    }
}