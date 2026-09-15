using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace UltraSol.Shared.Infrastructure.Persistence.PostgreSQL
{
    public class PostgreSqlConnectionChecker : IHostedService
    {
        private readonly PostgreSqlOptions _options;
        private readonly ILogger<PostgreSqlConnectionChecker> _logger;

        public PostgreSqlConnectionChecker(
            PostgreSqlOptions options,
            ILogger<PostgreSqlConnectionChecker> logger)
        {
            _options = options;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                _logger.LogInformation("Database connected successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection failed.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}