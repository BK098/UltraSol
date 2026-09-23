using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltraSol.Modules.Pricing.Domain.Repositories;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Exceptions;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Pricing.Infrastructure.Repositories;

public sealed class PricingUnitOfWork(PricingDbContext context, IDomainEventDispatcher dispatcher) : UnitOfWork(context, dispatcher), IPricingUnitOfWork
{
    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Translate(() => base.SaveChangesAsync(cancellationToken));
    public new Task<int> SaveChangesAsync(bool dispatchDomainEvents, CancellationToken cancellationToken = default) => Translate(() => base.SaveChangesAsync(dispatchDomainEvents, cancellationToken));

    public new async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        context.ClearContractLocks();
        return new PricingTransaction(await base.BeginTransactionAsync(cancellationToken), context);
    }

    public new async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async ct =>
        {
            await action(ct);
            return 0;
        }, cancellationToken);
    }

    public new async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken = default)
    {
        context.ClearContractLocks();
        try
        {
            return await Translate(() => base.ExecuteInTransactionAsync(action, cancellationToken));
        }
        finally
        {
            context.ClearContractLocks();
        }
    }

    private static async Task<T> Translate<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (ConflictCode(exception) is not null)
        {
            throw new DomainException("Pricing changed concurrently or conflicts with an existing record. Reload and retry.", exception, ConflictCode(exception));
        }
    }

    private static string? ConflictCode(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return "PricingConcurrencyConflict";
        }
        if (exception is PostgresException postgres)
        {
            return postgres.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation => "PricingUniqueConflict",
                PostgresErrorCodes.ExclusionViolation => "OverlappingPricePeriods",
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected => "PricingConcurrencyConflict",
                PostgresErrorCodes.CheckViolation => "PricingConstraintViolation",
                _ => null
            };
        }
        return exception.InnerException is null ? null : ConflictCode(exception.InnerException);
    }

    private sealed class PricingTransaction(ITransaction transaction, PricingDbContext context) : ITransaction
    {
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await Translate(async () =>
            {
                await transaction.CommitAsync(cancellationToken);
                return 0;
            });
            context.ClearContractLocks();
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ClearContractLocks();
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            context.ClearContractLocks();
        }
    }
}