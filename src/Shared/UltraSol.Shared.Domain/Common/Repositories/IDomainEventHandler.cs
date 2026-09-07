using UltraSol.Shared.Domain.Common.Abstractions;

namespace UltraSol.Shared.Domain.Common.Repositories
{
    public interface IDomainEventHandler
    {
        Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    }
}