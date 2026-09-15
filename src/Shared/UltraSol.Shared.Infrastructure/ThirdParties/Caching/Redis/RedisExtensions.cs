using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using UltraSol.Shared.Application.Caching;

namespace UltraSol.Shared.Infrastructure.ThirdParties.Caching.Redis
{
    internal static class RedisExtensions
    {
        internal static IServiceCollection AddRedis(this IServiceCollection services)
        {
            var options = services.GetOptions<RedisOptions>(RedisOptions.SectionName);
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException("Redis ConnectionString is required.");
            }
            services.AddSingleton(options);
            services.AddHostedService<RedisConnectionChecker>();
            services.AddSingleton<IOptions<RedisOptions>>(Options.Create(options));
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options.ConnectionString));
            services.AddSingleton<ICacheService, RedisCacheService>();
            return services;
        }
    }
}