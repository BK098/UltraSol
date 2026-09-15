using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace UltraSol.Shared.Infrastructure.ThirdParties.Caching.Redis
{
    public sealed class RedisConnectionChecker(RedisOptions options, ILogger<RedisConnectionChecker> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await using var connection = await ConnectionMultiplexer.ConnectAsync(options.ConnectionString);

                var database = connection.GetDatabase();
                var latency = await database.PingAsync();

                logger.LogInformation("Redis connected successfully. Ping: {Latency} ms", latency.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to Redis.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}