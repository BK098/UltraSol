using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace UltraSol.Shared.Infrastructure.Persistence.PostgreSQL
{
    public sealed class PostgreSqlConnectionMonitor(PostgreSqlOptions options, ILogger<PostgreSqlConnectionMonitor> logger)
    : BackgroundService
    {
        private const int MaxFailureCount = 3;
        private int _failureCount;

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var connection =
                        new NpgsqlConnection(options.ConnectionString);

                    await connection.OpenAsync(stoppingToken);

                    // Có thể ping DB thật sự nếu muốn
                    await using var command =
                        new NpgsqlCommand("SELECT 1", connection);

                    await command.ExecuteScalarAsync(stoppingToken);

                    if (_failureCount > 0)
                    {
                        logger.LogInformation("PostgreSQL connection recovered.");
                    }
                    _failureCount = 0;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _failureCount++;
                    logger.LogError(
                        ex,
                        "PostgreSQL connection failed. Attempt {Attempt}/{MaxAttempts}.",
                        _failureCount,
                        MaxFailureCount);

                    if (_failureCount >= MaxFailureCount)
                    {
                        logger.LogCritical(
                            "PostgreSQL connection failed {Count} consecutive times. Terminating application.",
                            _failureCount);
                        Environment.Exit(1);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}