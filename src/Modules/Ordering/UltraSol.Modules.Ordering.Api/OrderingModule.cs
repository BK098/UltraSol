using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Carts;
using UltraSol.Modules.Ordering.Application.Features.Orders;
using UltraSol.Modules.Ordering.Application.Features.Payments;
using UltraSol.Modules.Ordering.Application.Messaging;
using UltraSol.Modules.Ordering.Domain.Repositories;
using UltraSol.Modules.Ordering.Infrastructure.Checkout;
using UltraSol.Modules.Ordering.Infrastructure.Integrations;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using UltraSol.Modules.Ordering.Infrastructure.Reads;
using UltraSol.Modules.Ordering.Infrastructure.Repositories;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.Infrastructure.DependencyInjections;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;
using UltraSol.Shared.IntegrationEvents.Fulfillment;
using UltraSol.Shared.IntegrationEvents.Inventory;

[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
[assembly: InternalsVisibleTo("UltraSol.Modules.Ordering.Tests")]

namespace UltraSol.Modules.Ordering.Api;

internal static class OrderingModule
{
    public static IServiceCollection AddOrderingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddControllers().AddApplicationPart(typeof(OrderingModule).Assembly);
        services.AddPostgres<OrderingDbContext>("ordering");
        _ = typeof(CheckoutOrchestrator).Assembly;
        services.AddRegistration(AppDomain.CurrentDomain.GetAssemblies());
        services.AddScoped<IOrderingUnitOfWork, OrderingUnitOfWork>();
        services.AddScoped<IOrderingStore, OrderingStore>();
        services.AddScoped<IOrderingReadStore, OrderingReadStore>();
        services.AddScoped<OrderingAccess>();
        services.AddScoped<CartService>();
        services.AddScoped<CheckoutOrchestrator>();
        services.AddScoped<OrderLifecycle>();
        services.AddScoped<OrderReadService>();
        services.AddScoped<OrderPaymentFacade>();
        services.AddScoped<IOrderingOutbox, UltraSol.Modules.Ordering.Infrastructure.Messaging.OrderingOutbox>();
        services.AddOptions<OrderingOptions>().Bind(configuration.GetSection("Ordering"))
            .Validate(x => x.ReservationMinutes is > 0 and <= 1440, "Ordering reservation duration must be between 1 and 1440 minutes.").ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OrderingOptions>>().Value);
        services.AddSingleton<OrderingServiceToken>();
        services.AddTransient<OrderingServiceAuthHandler>();
        ConfigureClient(services.AddHttpClient("ordering-auth"), () => configuration["Ordering:AuthApi:BaseUrl"]);
        ConfigureClient(services.AddHttpClient<ICatalogCheckoutClient, CatalogCheckoutClient>(), () => configuration["Ordering:CatalogApi:BaseUrl"])
            .AddHttpMessageHandler<OrderingServiceAuthHandler>();
        ConfigureClient(services.AddHttpClient<IPricingQuoteClient, PricingQuoteClient>(), () => configuration["Ordering:PricingApi:BaseUrl"])
            .AddHttpMessageHandler<OrderingServiceAuthHandler>();
        ConfigureClient(services.AddHttpClient<IInventoryReservationClient, InventoryReservationClient>(), () => configuration["Ordering:InventoryApi:BaseUrl"])
            .AddHttpMessageHandler<OrderingServiceAuthHandler>();
        ConfigureClient(services.AddHttpClient<IOrderPaymentClient, OrderPaymentClient>(), () => configuration["Ordering:PaymentApi:BaseUrl"])
            .AddHttpMessageHandler<OrderingServiceAuthHandler>();
        services.AddScoped<Mailbox<OrderingDbContext>>(provider => new(provider.GetRequiredService<OrderingDbContext>(), provider.GetRequiredService<IOrderingUnitOfWork>(), provider));
        services.AddScoped<IIntegrationHandler<InventoryReservationExpiredV1>, ReservationExpiredHandler>();
        services.AddScoped<IIntegrationHandler<FulfillmentCompletedV1>, FulfillmentCompletedHandler>();
        services.AddSingleton(new ModuleMailbox("ordering", provider => provider.GetRequiredService<Mailbox<OrderingDbContext>>(),
            [typeof(InventoryReservationExpiredV1), typeof(FulfillmentCompletedV1)]));
        services.AddHostedService<CheckoutRecoveryWorker>();
        return services;
    }

    public static async Task<IApplicationBuilder> UseOrderingModule(this IApplicationBuilder app)
    {
        await app.ApplicationServices.MigrateAsync<OrderingDbContext>();
        return app;
    }

    private static IHttpClientBuilder ConfigureClient(IHttpClientBuilder builder, Func<string?> address) => builder.ConfigureHttpClient(client =>
    {
        var baseUrl = address();
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        }
        client.Timeout = TimeSpan.FromSeconds(10);
    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
}
