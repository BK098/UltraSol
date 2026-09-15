using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UltraSol.Modules.Inventory.Application;
using UltraSol.Modules.Inventory.Application.Contracts;
using UltraSol.Modules.Inventory.Domain;
using UltraSol.Modules.Inventory.Domain.Repositories;
using UltraSol.Modules.Inventory.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Exceptions;

namespace UltraSol.Modules.Inventory.Infrastructure;

public sealed class InventoryModuleService(IServiceScopeFactory scopes) : IInventoryModule
{
    public Task<ReceiptResult> ReceiveAsync(ReceiveStockRequest request, string? actorId, CancellationToken ct = default) =>
        WriteAsync((operations, token) => operations.ReceiveAsync(request, actorId, token), (operations, token) => operations.FindReceiptReplayAsync(request, token), ct);

    public Task<ReservationResult> ReserveAsync(ReserveStockRequest request, string? actorId, CancellationToken ct = default) =>
        WriteAsync((operations, token) => operations.ReserveAsync(request, actorId, token), (operations, token) => operations.FindReservationReplayAsync(request, token), ct);

    public Task<ReservationResult> ReleaseByOrderAsync(Guid orderId, string? actorId, CancellationToken ct = default) =>
        WriteAsync((operations, token) => operations.ReleaseByOrderAsync(orderId, actorId, token), (operations, token) => operations.FindReservationTerminalReplayAsync(Guid.Empty, orderId, false, token), ct);

    public Task<ReservationResult> ReleaseAsync(Guid reservationId, string? actorId, CancellationToken ct = default) =>
        WriteAsync((operations, token) => operations.ReleaseAsync(reservationId, actorId, token), (operations, token) => operations.FindReservationTerminalReplayAsync(reservationId, null, false, token), ct);

    public Task<ReservationResult> IssueAsync(Guid reservationId, string? actorId, CancellationToken ct = default) =>
        WriteAsync((operations, token) => operations.IssueAsync(reservationId, actorId, token), (operations, token) => operations.FindReservationTerminalReplayAsync(reservationId, null, true, token), ct);

    public Task<AdjustmentResult> CreateAdjustmentAsync(CreateAdjustmentRequest request, string? actorId, CancellationToken ct = default) =>
        WriteAsync((operations, token) => operations.CreateAdjustmentAsync(request, actorId, token), null, ct);

    public Task<AdjustmentResult> UpdateAdjustmentAsync(Guid adjustmentId, UpdateAdjustmentRequest request, string? actorId, CancellationToken ct = default) =>
        WriteAsync((operations, token) => operations.UpdateAdjustmentAsync(adjustmentId, request, actorId, token), null, ct);

    public Task<AdjustmentResult> PostAdjustmentAsync(Guid adjustmentId, string? actorId, CancellationToken ct = default) =>
        WriteAsync((operations, token) => operations.PostAdjustmentAsync(adjustmentId, actorId, token), (operations, token) => operations.FindAdjustmentTerminalReplayAsync(adjustmentId, true, token), ct);

    public Task<AdjustmentResult> CancelAdjustmentAsync(Guid adjustmentId, string? actorId, CancellationToken ct = default) =>
        WriteAsync((operations, token) => operations.CancelAdjustmentAsync(adjustmentId, actorId, token), (operations, token) => operations.FindAdjustmentTerminalReplayAsync(adjustmentId, false, token), ct);

    public async Task<IReadOnlyList<AvailabilityResult>> GetAvailabilityAsync(IReadOnlyList<Guid> productItemIds, CancellationToken ct = default)
    {
        if (productItemIds is null || productItemIds.Count is < 1 or > 200 || productItemIds.Any(id => id == Guid.Empty))
        {
            throw new ValidationException("Supply between 1 and 200 nonempty ProductItemIds.");
        }
        var ids = productItemIds.Distinct().ToArray();
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var rows = await db.Stocks.AsNoTracking().Where(stock => stock.WarehouseId == InventoryDefaults.WarehouseId && ids.Contains(stock.ProductItemId))
            .Select(stock => new AvailabilityResult(stock.WarehouseId, stock.ProductItemId, stock.OnHand, stock.Reserved, stock.OnHand - stock.Reserved)).ToDictionaryAsync(stock => stock.ProductItemId, ct);
        return ids.Select(id => rows.GetValueOrDefault(id) ?? new AvailabilityResult(InventoryDefaults.WarehouseId, id, 0, 0, 0)).ToArray();
    }

    private async Task<T> WriteAsync<T>(Func<InventoryOperations, CancellationToken, Task<T>> action, Func<InventoryOperations, CancellationToken, Task<T?>>? replay, CancellationToken ct)
        where T : class
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var operations = scope.ServiceProvider.GetRequiredService<InventoryOperations>();
            var unit = scope.ServiceProvider.GetRequiredService<IInventoryUnitOfWork>();
            return await unit.ExecuteInTransactionAsync(token => action(operations, token), ct);
        }
        catch (Exception exception) when (IsWriteConflict(exception))
        {
            // Read the committed winner using a new context; never replay a failed mutation.
            if (replay is not null)
            {
                await using var fresh = scopes.CreateAsyncScope();
                var result = await replay(fresh.ServiceProvider.GetRequiredService<InventoryOperations>(), ct);
                if (result is not null)
                {
                    return result;
                }
            }
            throw new ConcurrencyException("Inventory", null);
        }
    }

    internal static bool IsWriteConflict(Exception exception) => exception is DbUpdateConcurrencyException ||
        (exception is PostgresException postgres && postgres.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected) ||
        (exception is DbUpdateException update && update.InnerException is PostgresException inner && inner.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected);
}
