using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltraSol.Modules.Payment.Application;
using UltraSol.Modules.Payment.Application.Contracts;
using UltraSol.Modules.Payment.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Repositories;

namespace UltraSol.Modules.Payment.Infrastructure.Repositories;

public sealed class PaymentUnitOfWork(PaymentDbContext context, IDomainEventDispatcher dispatcher)
    : UnitOfWork(context, dispatcher), IPaymentUnitOfWork
{
    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Translate(() => base.SaveChangesAsync(cancellationToken));

    public new Task<int> SaveChangesAsync(bool dispatchDomainEvents, CancellationToken cancellationToken = default) =>
        Translate(() => base.SaveChangesAsync(dispatchDomainEvents, cancellationToken));

    public new async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        new PaymentTransaction(await Translate(() => base.BeginTransactionAsync(cancellationToken)));

    public new Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) =>
        Translate(() => base.ExecuteInTransactionAsync(action, cancellationToken));

    public new Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default) =>
        Translate(() => base.ExecuteInTransactionAsync(action, cancellationToken));

    private static async Task Translate(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (IsConflict(exception))
        {
            throw new PaymentFailure(409, "PaymentConflict", "Payment data changed or conflicts with an existing record.");
        }
    }

    private static async Task<T> Translate<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception exception) when (IsConflict(exception))
        {
            throw new PaymentFailure(409, "PaymentConflict", "Payment data changed or conflicts with an existing record.");
        }
    }

    private static bool IsConflict(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return true;
        }
        if (exception is PostgresException postgres)
        {
            return postgres.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or
                PostgresErrorCodes.DeadlockDetected;
        }
        return exception.InnerException is not null && IsConflict(exception.InnerException);
    }

    private sealed class PaymentTransaction(ITransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Translate(() => transaction.CommitAsync(cancellationToken));
        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
