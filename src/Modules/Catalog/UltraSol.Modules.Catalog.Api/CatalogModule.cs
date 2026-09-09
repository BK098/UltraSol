using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Runtime.CompilerServices;
using UltraSol.Modules.Catalog.Application.Features.Products.Commands;
using UltraSol.Modules.Catalog.Domain.Abstractions;
using UltraSol.Modules.Catalog.Domain.Catalog.Categories;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Modules.Catalog.Infrastructure.Repositories;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.DependencyInjections;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;

[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
[assembly: InternalsVisibleTo("UltraSol.Modules.Catalog.Infrastructure.Tests")]
namespace UltraSol.Modules.Catalog.Api;

internal static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPostgres<CatalogDbContext>(true);
        services.AddRegistration(AppDomain.CurrentDomain.GetAssemblies());
        services.AddScoped<ICatalogUnitOfWork, CatalogUnitOfWork>();
        services.AddScoped<IDomainEventHandler, ProductPublishedHandler>();
        services.AddScoped<ICategoryHierarchyReader, CategoryHierarchyReader>();
        services.AddScoped<CategoryHierarchyService>();
        return services;
    }

    public static IApplicationBuilder UseCatalogModule(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        return app;
    }
}