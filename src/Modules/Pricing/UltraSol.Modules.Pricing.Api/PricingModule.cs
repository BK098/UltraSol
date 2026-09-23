using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Runtime.CompilerServices;
using UltraSol.Modules.Pricing.Application.Features.SkuPrices.Services;
using UltraSol.Modules.Pricing.Application.Integrations.Catalog;
using UltraSol.Modules.Pricing.Application.Reads;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Infrastructure.Integrations.Catalog;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Modules.Pricing.Infrastructure.Reads;
using UltraSol.Modules.Pricing.Infrastructure.Repositories;
using UltraSol.Shared.Infrastructure.DependencyInjections;
using UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;

[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
[assembly: InternalsVisibleTo("UltraSol.Modules.Pricing.Tests")]

namespace UltraSol.Modules.Pricing.Api;

internal static class PricingModule
{
    public static IServiceCollection AddPricingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddPostgres<PricingDbContext>(Schema.Name);
        services.AddRegistration(AppDomain.CurrentDomain.GetAssemblies());
        services.AddHttpContextAccessor();
        services.AddHttpClient<ICatalogSkuClient, CatalogSkuClient>(client =>
        {
            var address = configuration["Pricing:CatalogApi:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(address))
            {
                client.BaseAddress = new Uri(address.TrimEnd('/') + "/", UriKind.Absolute);
            }
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
        //services.TryAddSingleton<IAuthorizationMiddlewareResultHandler, PricingAuthorizationResultHandler>();
        services.AddScoped<SkuPriceWriter>();
        services.AddScoped<IPricingUnitOfWork, PricingUnitOfWork>();
        services.AddScoped<IPricingReadStore, PricingReadStore>();
        services.AddScoped<UltraSol.Modules.Pricing.Application.Features.Quotes.Services.PricingQuoteService,
            UltraSol.Modules.Pricing.Infrastructure.Quotes.PricingQuoteService>();
        //services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services;
    }

    public static async Task<IApplicationBuilder> UsePricingModule(this IApplicationBuilder app)
    {
        await app.ApplicationServices.MigrateAsync<PricingDbContext>();
        return app;
    }
}

//internal sealed class PricingAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
//{
//    private readonly AuthorizationMiddlewareResultHandler _default = new();

//    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
//    {
//        if (context.Request.Path.StartsWithSegments("/api/pricing") && (result.Challenged || result.Forbidden))
//        {
//            context.Response.StatusCode = result.Challenged ? 401 : 403;
//            var response = result.Challenged ? ApiResultBuilder.Unauthorized<object>() : ApiResultBuilder.Forbidden<object>();
//            return context.Response.WriteAsJsonAsync(response, context.RequestAborted);
//        }
//        return _default.HandleAsync(next, context, policy, result);
//    }
//}