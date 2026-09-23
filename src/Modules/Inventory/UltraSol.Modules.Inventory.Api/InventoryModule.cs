using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Runtime.CompilerServices;
using UltraSol.Modules.Inventory.Application;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Application.Reads;
using UltraSol.Modules.Inventory.Domain.Repositories;
using UltraSol.Modules.Inventory.Infrastructure;
using UltraSol.Modules.Inventory.Infrastructure.Messaging;
using UltraSol.Modules.Inventory.Infrastructure.Persistence;
using UltraSol.Modules.Inventory.Infrastructure.Reads;
using UltraSol.Modules.Inventory.Infrastructure.Repositories;
using UltraSol.Shared.Infrastructure.DependencyInjections;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;

[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
[assembly: InternalsVisibleTo("UltraSol.Modules.Inventory.Tests")]

namespace UltraSol.Modules.Inventory.Api;

internal static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        //services.AddControllers().AddApplicationPart(typeof(InventoryModule).Assembly);
        services.AddPostgres<InventoryDbContext>(Schema.Name); //Db ở đây
        services.TryAddSingleton(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddRegistration(AppDomain.CurrentDomain.GetAssemblies());
        services.AddScoped<InventoryOperations>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IInventoryUnitOfWork, InventoryUnitOfWork>();
        services.AddScoped<IInventoryOutbox, InventoryOutbox>();
        services.AddScoped<IInventoryModule, InventoryModuleService>();
        services.AddScoped<IInventoryReadStore, InventoryReadStore>();
        services.AddScoped<Mailbox<InventoryDbContext>>(provider => new(provider.GetRequiredService<InventoryDbContext>(), provider.GetRequiredService<IInventoryUnitOfWork>(), provider));
        services.AddSingleton(new ModuleMailbox("inventory", provider => provider.GetRequiredService<Mailbox<InventoryDbContext>>(), []));
        services.AddHostedService<ReservationExpiryWorker>();
        return services;
    }
    public static async Task<IApplicationBuilder> UseInventoryModule(
    this IApplicationBuilder app)
    {
        await app.ApplicationServices.MigrateAsync<InventoryDbContext>();
        return app;
    }
}