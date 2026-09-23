using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UltraSol.Modules.Ordering.Application.Checkout;
using UltraSol.Modules.Ordering.Application.Features.Orders;

namespace UltraSol.Modules.Ordering.Infrastructure.Checkout;

public sealed class CheckoutRecoveryWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<CheckoutRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), clock);
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
            catch (Exception error)
            {
                logger.LogWarning("Ordering recovery will retry ({ErrorType}).", error.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task SweepAsync(CancellationToken ct)
    {
        Guid[] attempts;
        Guid[] operations;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOrderingStore>();
            attempts = await store.PendingAttemptsAsync(clock.GetUtcNow(), ct);
            operations = await store.PendingOperationsAsync(clock.GetUtcNow(), ct);
        }
        // ponytail: sequential batches of 20; partition workers if recovery throughput becomes limiting.
        foreach (var id in attempts)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<CheckoutOrchestrator>().RunAsync(id, ct);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Checkout {OperationId} retained for recovery ({ErrorType}).", id, error.GetType().Name);
            }
        }
        foreach (var id in operations)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OrderLifecycle>().RunAsync(id, ct);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Order operation {OperationId} retained for recovery ({ErrorType}).", id, error.GetType().Name);
            }
        }
    }
}