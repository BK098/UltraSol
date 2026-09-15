using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UltraSol.Modules.Inventory.Application;
using UltraSol.Modules.Inventory.Application.Reads;
using UltraSol.Modules.Inventory.Domain.Repositories;

namespace UltraSol.Modules.Inventory.Infrastructure;

public sealed class ReservationExpiryWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), time);
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Inventory reservation expiry sweep failed; will retry next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task SweepAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Guid> ids;
        await using (var scope = scopes.CreateAsyncScope())
        {
            ids = await scope.ServiceProvider.GetRequiredService<IInventoryReadStore>().ExpiredReservationIdsAsync(time.GetUtcNow(), 100, ct);
        }
        foreach (var id in ids)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var operations = scope.ServiceProvider.GetRequiredService<InventoryOperations>();
                await scope.ServiceProvider.GetRequiredService<IInventoryUnitOfWork>().ExecuteInTransactionAsync(token => operations.ExpireAsync(id, token), ct);
            }
            catch (Exception exception) when (InventoryModuleService.IsWriteConflict(exception))
            {
                logger.LogDebug("Reservation {ReservationId} changed during expiry; it will be checked on the next sweep.", id);
            }
        }
    }
}
