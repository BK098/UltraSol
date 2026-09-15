using Microsoft.EntityFrameworkCore;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.IntegrationEvents.Auth;
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
using UltraSol.Modules.Catalog.Application.Reads;

[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
[assembly: InternalsVisibleTo("UltraSol.Modules.Catalog.Infrastructure.Tests")]
namespace UltraSol.Modules.Catalog.Api;

internal static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPostgres<CatalogDbContext>(Schema.Name);
        services.ConfigureDbContext<CatalogDbContext>(options => options.UseNpgsql(postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", Schema.Name)));
        services.AddRegistration([typeof(CreateProductCommand).Assembly, typeof(ProductRepository).Assembly]);
        services.AddScoped<ICatalogUnitOfWork, CatalogUnitOfWork>();
        services.AddCatalogReads();
        services.AddScoped<IClientCatalogReadStore, ClientCatalogReadStore>();
        services.AddScoped<Mailbox<CatalogDbContext>>(sp => new(sp.GetRequiredService<CatalogDbContext>(), sp.GetRequiredService<ICatalogUnitOfWork>(), sp));
        services.AddSingleton(new ModuleMailbox("catalog", sp => sp.GetRequiredService<Mailbox<CatalogDbContext>>(), []));
        services.AddScoped<IDomainEventHandler, ProductPublishedHandler>();
        services.AddScoped<ICategoryHierarchyReader, CategoryHierarchyReader>();
        services.AddScoped<CategoryHierarchyService>();
        //services.AddScoped<CatalogSeeder>();
        return services;
    }


    public static async Task<IApplicationBuilder> UseCatalogModule(this IApplicationBuilder app)
    {
        await app.ApplicationServices.MigrateAsync<CatalogDbContext>();
        
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