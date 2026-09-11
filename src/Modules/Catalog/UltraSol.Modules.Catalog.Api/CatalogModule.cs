using Microsoft.AspNetCore.Builder;
using UltraSol.Modules.Catalog.Infrastructure.Reads;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UltraSol.Modules.Catalog.Infrastructure.Persistence.Seeding;
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
        services.AddCatalogReads();
        services.AddScoped<IDomainEventHandler, ProductPublishedHandler>();
        services.AddScoped<ICategoryHierarchyReader, CategoryHierarchyReader>();
        services.AddScoped<CategoryHierarchyService>();
        services.AddScoped<CatalogSeeder>();
        return services;
    }

    public static IApplicationBuilder UseCatalogModule(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        return app;
    }

    public static async Task SeedCatalogAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<CatalogSeeder>();
        var inserted = await seeder.SeedAsync(Path.Combine(AppContext.BaseDirectory, "seeds", "catalog.json"), app.Lifetime.ApplicationStopping);
        app.Logger.LogInformation(inserted ? "Catalog seed inserted successfully." : "Catalog seed SKUs already exist; skipped.");
    }
}