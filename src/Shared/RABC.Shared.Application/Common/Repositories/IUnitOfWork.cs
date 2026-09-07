namespace Domain.Common.Repositories
{
    public interface IUnitOfWork : IDisposable, IAsyncDisposable
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}