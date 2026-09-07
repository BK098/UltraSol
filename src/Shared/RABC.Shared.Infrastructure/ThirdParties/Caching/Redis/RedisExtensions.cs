using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using UltraSol.Shared.Application.Caching;

namespace UltraSol.Shared.Infrastructure.ThirdParties.Caching.Redis
{
    internal static class RedisExtensions
    {
        internal static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
        {
            var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
            if (options == null)
            {
                throw new InvalidOperationException(
                        "Redis configuration is missing.");
            }
            services.AddOptions<RedisOptions>()
                .Bind(configuration.GetSection(RedisOptions.SectionName))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                    "Redis ConnectionString is required.")
                .ValidateOnStart();
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var options = sp
                    .GetRequiredService<IOptions<RedisOptions>>()
                    .Value;

                return ConnectionMultiplexer.Connect(
                    options.ConnectionString);
            });
            services.AddSingleton<ICacheService, RedisCacheService>();
            return services;
        }
    }
}