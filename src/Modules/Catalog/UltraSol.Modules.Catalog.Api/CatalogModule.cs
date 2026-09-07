using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Runtime.CompilerServices;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Domain.Catalog.Products;
using UltraSol.Modules.Catalog.Domain.Catalog.ProductItems;
using UltraSol.Modules.Catalog.Domain.Catalog.Brands;
using UltraSol.Shared.Domain.Common.Abstractions;
using CatalogCollection = UltraSol.Modules.Catalog.Domain.Catalog.Collections.Collection;
using UltraSol.Modules.Catalog.Domain.Repositories;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Modules.Catalog.Infrastructure.Repositories;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;
using UltraSol.Shared.Infrastructure.Repositories;

[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
[assembly: InternalsVisibleTo("UltraSol.Modules.Catalog.Infrastructure.Tests")]
namespace UltraSol.Modules.Catalog.Api;

internal static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPostgres<CatalogDbContext>(configuration, CatalogPostgresConfiguration.Configure);
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductItemRepository, ProductItemRepository>();
        services.AddScoped<IBrandRepository, BrandRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();
        services.AddScoped<ICollectionProductQuery, CollectionProductQuery>();
        BindSharedRepository<Product, IProductRepository>(services);
        BindSharedRepository<ProductItem, IProductItemRepository>(services);
        BindSharedRepository<Brand, IBrandRepository>(services);
        BindSharedRepository<Category, ICategoryRepository>(services);
        BindSharedRepository<CatalogCollection, ICollectionRepository>(services);
        services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        // Keyed registration avoids accidentally using Auth's DbContext as modules are added.
        services.AddKeyedScoped<IUnitOfWork>(Schema.Name, (provider, _) =>
            new UnitOfWork(provider.GetRequiredService<CatalogDbContext>(), provider.GetRequiredService<IDomainEventDispatcher>()));
        services.AddScoped<ICategoryHierarchyReader, CategoryHierarchyReader>();
        services.AddScoped<CategoryHierarchyService>();
        return services;
    }

    private static void BindSharedRepository<TEntity, TContract>(IServiceCollection services)
        where TEntity : class, IEntity<Guid>
        where TContract : class, IRepository<TEntity>
    {
        services.AddScoped<IRepository<TEntity>>(provider => provider.GetRequiredService<TContract>());
        services.AddScoped<IQueryRepository<TEntity>>(provider => provider.GetRequiredService<TContract>());
        services.AddScoped<ICommandRepository<TEntity>>(provider => provider.GetRequiredService<TContract>());
    }

    public static async Task<IApplicationBuilder> UseCatalogModuleAsync(this IApplicationBuilder app,
        CancellationToken cancellationToken = default)
    {
        if (app.ApplicationServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            await using var scope = app.ApplicationServices.CreateAsyncScope();
            await CatalogMigrator.MigrateAsync(scope.ServiceProvider.GetRequiredService<CatalogDbContext>(), cancellationToken);
        }
        return app;
    }
}
