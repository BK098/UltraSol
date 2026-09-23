using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UltraSol.Modules.Payment.Application;

namespace UltraSol.Modules.Payment.Infrastructure.Recovery;

public sealed class PaymentRecoveryWorker(IServiceScopeFactory scopes, ILogger<PaymentRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                Guid[] orders;
                await using (var scope = scopes.CreateAsyncScope())
                {
                    orders = await scope.ServiceProvider.GetRequiredService<PaymentRecovery>().PendingAsync(stoppingToken);
                }
                foreach (var orderId in orders)
                {
                    await RecoverAsync(orderId, false, stoppingToken);
                    await RecoverAsync(orderId, true, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                logger.LogWarning("Payment recovery batch deferred ({ErrorType}).", error.GetType().Name);
            }
        }
    }

    private async Task RecoverAsync(Guid orderId, bool refund, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var recovery = scope.ServiceProvider.GetRequiredService<PaymentRecovery>();
            if (refund)
            {
                await recovery.RefundAsync(orderId, ct);
            }
            else
            {
                await recovery.QueryAsync(orderId, null, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            logger.LogWarning("Payment recovery deferred for order {OrderId} ({ErrorType}).", orderId, error.GetType().Name);
        }
    }
}
