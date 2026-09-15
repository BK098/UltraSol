using UltraSol.Shared.Domain.Common.Abstractions;

namespace UltraSol.Shared.Domain.Common.Repositories
{
    public interface IDomainEventDispatcher
    {
        Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
    }
}