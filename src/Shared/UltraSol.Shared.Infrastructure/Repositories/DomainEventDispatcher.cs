using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Repositories;

namespace UltraSol.Shared.Infrastructure.Repositories;

public sealed class DomainEventDispatcher(IEnumerable<IDomainEventHandler> handlers) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
            foreach (var handler in handlers)
                await handler.HandleAsync(domainEvent, cancellationToken).ConfigureAwait(false);
    }
}

