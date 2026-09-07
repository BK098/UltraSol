using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using UltraSol.Shared.Infrastructure.ThirdParties.Caching.Redis;

[assembly: InternalsVisibleTo("UltraSol.Bootstrappers")]
namespace UltraSol.Shared.Infrastructure
{
    internal static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddRedis(configuration);
            return services;
        }
        public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder app)
        {
            return app;
        }
    }
}