using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using UltraSol.Shared.Application.Caching;

namespace UltraSol.Shared.Infrastructure.ThirdParties.Caching.Redis
{
    internal sealed class RedisCacheService(IConnectionMultiplexer connection, IOptions<RedisOptions> options) : ICacheService
    {
        private readonly IDatabase _database = connection.GetDatabase();
        private readonly string _instanceName = options.Value.InstanceName;
        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            var value = await _database.StringGetAsync(GetKey(key));
            if (value.IsNullOrEmpty)
            {
                return default;
            }
            return JsonSerializer.Deserialize<T>(value.ToString());
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(value);

            await _database.StringSetAsync(GetKey(key), json, expiry: expiration, when: When.Always, flags: CommandFlags.None);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            await _database.KeyDeleteAsync(GetKey(key));
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _database.KeyExistsAsync(GetKey(key));
        }

        private string GetKey(string key) => $"{_instanceName}{key}";
    }
}