namespace UltraSol.Shared.Infrastructure.ThirdParties.Caching.Redis
{
    internal class RedisOptions
    {
        public const string SectionName = "Redis";
        public string ConnectionString { get; init; } = string.Empty;
        public string InstanceName { get; init; } = string.Empty;
    }
}