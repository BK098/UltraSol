using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltraSol.Modules.Payment.Application;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Application.Messaging;
using UltraSol.Modules.Payment.Infrastructure.Integrations;
using UltraSol.Modules.Payment.Infrastructure.Persistence;
using UltraSol.Modules.Payment.Infrastructure.Repositories;
using UltraSol.Modules.Payment.Infrastructure.Recovery;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.Infrastructure.DependencyInjections;
using UltraSol.Shared.Infrastructure.Messaging;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;
using UltraSol.Shared.IntegrationEvents.Ordering;

[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
[assembly: InternalsVisibleTo("UltraSol.Modules.Payment.Tests")]

namespace UltraSol.Modules.Payment.Api;

internal static class PaymentModule
{
    public static IServiceCollection AddPaymentModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddControllers().AddApplicationPart(typeof(PaymentModule).Assembly);
        services.AddPostgres<PaymentDbContext>("payment");
        _ = typeof(PaymentService).Assembly;
        services.AddRegistration(AppDomain.CurrentDomain.GetAssemblies());
        services.AddScoped<IPaymentStore, PaymentStore>();
        services.AddScoped<IPaymentUnitOfWork, PaymentUnitOfWork>();
        services.AddScoped<PaymentService>();
        services.AddScoped<PaymentRecovery>();
        services.AddScoped<OrderPaymentHandlers>();
        services.AddScoped<IIntegrationHandler<OrderPlacedV1>>(provider => provider.GetRequiredService<OrderPaymentHandlers>());
        services.AddScoped<IIntegrationHandler<OrderCancelledV1>>(provider => provider.GetRequiredService<OrderPaymentHandlers>());
        services.AddScoped<IIntegrationHandler<OrderRejectedV1>>(provider => provider.GetRequiredService<OrderPaymentHandlers>());
        services.AddScoped<IIntegrationHandler<OrderCompletedV1>>(provider => provider.GetRequiredService<OrderPaymentHandlers>());
        services.AddScoped<Mailbox<PaymentDbContext>>(provider => new(provider.GetRequiredService<PaymentDbContext>(), provider.GetRequiredService<IPaymentUnitOfWork>(), provider));
        services.AddSingleton(new ModuleMailbox("payment", provider => provider.GetRequiredService<Mailbox<PaymentDbContext>>(),
            [typeof(OrderPlacedV1), typeof(OrderCancelledV1), typeof(OrderRejectedV1), typeof(OrderCompletedV1)]));
        services.AddSingleton(configuration.GetSection("Payment:Vnpay").Get<VnpayOptions>() ?? new VnpayOptions());
        services.AddSingleton<PaymentServiceToken>();
        services.AddTransient<PaymentServiceAuthHandler>();
        Configure(services.AddHttpClient("payment-auth"), configuration["Payment:AuthApi:BaseUrl"]);
        Configure(services.AddHttpClient<IOrderingPaymentContextClient, OrderingPaymentContextClient>(), configuration["Payment:OrderingApi:BaseUrl"])
            .AddHttpMessageHandler<PaymentServiceAuthHandler>();
        Configure(services.AddHttpClient<IVnpayGateway, VnpayGateway>(), null).RemoveAllLoggers();
        services.AddHostedService<PaymentRecoveryWorker>();
        return services;
    }

    public static async Task<IApplicationBuilder> UsePaymentModule(this IApplicationBuilder app)
    {
        await app.ApplicationServices.MigrateAsync<PaymentDbContext>();
        return app;
    }

    private static IHttpClientBuilder Configure(IHttpClientBuilder builder, string? address) => builder.ConfigureHttpClient(client =>
    {
        if (!string.IsNullOrWhiteSpace(address))
        {
            client.BaseAddress = new Uri(address.TrimEnd('/') + "/", UriKind.Absolute);
        }
        client.Timeout = TimeSpan.FromSeconds(10);
    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
}
