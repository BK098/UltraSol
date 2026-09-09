using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Shared.Infrastructure.Repositories;

/// <summary>Owns database completion; events are dispatched only after a successful commit.</summary>
public sealed class EfTransaction : ITransaction
{
    private readonly IDbContextTransaction _transaction;
    private readonly Func<CancellationToken, Task> _afterCommit;
    private readonly Action _afterRollback;
    private bool _completed;

    public EfTransaction(IDbContextTransaction transaction) : this(transaction, _ => Task.CompletedTask, () => { }) { }
    internal EfTransaction(IDbContextTransaction transaction, Func<CancellationToken, Task> afterCommit, Action afterRollback)
    {
        _transaction = transaction;
        _afterCommit = afterCommit;
        _afterRollback = afterRollback;
    }
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
        await _afterCommit(cancellationToken).ConfigureAwait(false);
    }
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        _completed = true;
        _afterRollback();
    }
    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync().ConfigureAwait(false);
        if (!_completed) _afterRollback();
    }
}

public abstract class UnitOfWork(DbContext context, IDomainEventDispatcher domainEventDispatcher) : IUnitOfWork
{
    private readonly List<(IHasDomainEvents Owner, IDomainEvent Event)> _pendingEvents = [];
    private bool _ownsTransaction;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    public async Task<int> SaveChangesAsync(bool dispatchDomainEvents, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
        {
            var strategy = new SingleAttemptExecutionStrategy(context);
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await BeginTransactionAsync(cancellationToken);
                var count = await SaveInsideTransactionAsync(dispatchDomainEvents, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return count;
            });
        }
        if (dispatchDomainEvents && !_ownsTransaction)
            throw new InvalidOperationException("Use IUnitOfWork.BeginTransactionAsync to dispatch events after commit, or disable dispatch for an externally-owned transaction.");
        return await SaveInsideTransactionAsync(dispatchDomainEvents, cancellationToken);
    }

    private async Task<int> SaveInsideTransactionAsync(bool dispatchEvents, CancellationToken ct)
    {
        var events = dispatchEvents
            ? context.ChangeTracker.Entries<IHasDomainEvents>()
                .SelectMany(x => x.Entity.DomainEvents.Select(e => (Owner: x.Entity, Event: e))).ToArray()
            : [];
        var affected = await context.SaveChangesAsync(ct).ConfigureAwait(false);
        foreach (var pair in events)
            if (!_pendingEvents.Any(x => x.Event.EventId == pair.Event.EventId)) _pendingEvents.Add(pair);
        return affected;
    }

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("A transaction is already active.");
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (context is ITransactionPreparation preparation)
                await preparation.PrepareTransactionAsync(cancellationToken).ConfigureAwait(false);
            _ownsTransaction = true;
            return new EfTransaction(transaction, PublishCommittedEventsAsync, () =>
            {
                _pendingEvents.Clear();
                _ownsTransaction = false;
                // A rolled-back DbContext must not be reused as if its accepted changes were committed.
                context.ChangeTracker.Clear();
            });
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async ct => { await action(ct); return 0; }, cancellationToken);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        // This API captures scoped repositories/entities. Replaying it with the same context is unsafe.
        // Retry a failed operation at the caller boundary with a fresh scope and freshly read aggregates.
        var strategy = new SingleAttemptExecutionStrategy(context);
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await BeginTransactionAsync(cancellationToken);
            var result = await action(cancellationToken).ConfigureAwait(false);
            await SaveInsideTransactionAsync(true, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        });
    }

    private async Task PublishCommittedEventsAsync(CancellationToken ct)
    {
        _ownsTransaction = false;
        var events = _pendingEvents.ToArray();
        if (events.Length > 0)
            await domainEventDispatcher.DispatchAsync(events.Select(x => x.Event), ct).ConfigureAwait(false);
        foreach (var pair in events) pair.Owner.RemoveDomainEvent(pair.Event);
        _pendingEvents.Clear();
    }

    // DI owns the context; repositories and this UoW share its lifetime.
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

// Unlike NonRetryingExecutionStrategy, this enters EF's ambient strategy scope so nested
// commands under a provider configured with retries can participate in this transaction.
internal sealed class SingleAttemptExecutionStrategy(DbContext context) : ExecutionStrategy(context, 0, TimeSpan.Zero)
{
    protected override bool ShouldRetryOn(Exception exception) => false;
}
