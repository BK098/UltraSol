using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UltraSol.Modules.Auth.Infrastructure.Authorization;

public sealed class PermissionCacheWorker(
    IServiceScopeFactory scopes,
    ILogger<PermissionCacheWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await SynchronizeCatalogAsync(stoppingToken);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();

                await scope.ServiceProvider
                    .GetRequiredService<PermissionService>()
                    .SynchronizeAsync(stoppingToken);
            }
            catch (Exception exception)
                when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "Permission Redis synchronization failed ({ErrorType}); retrying from the committed version.",
                    exception.GetType().Name);
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task SynchronizeCatalogAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();

            await scope.ServiceProvider
                .GetRequiredService<PermissionCatalogSynchronizer>()
                .SynchronizeAsync(cancellationToken);
        }
        catch (Exception exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Initial permission catalog synchronization failed.");

            throw;
        }
    }
}