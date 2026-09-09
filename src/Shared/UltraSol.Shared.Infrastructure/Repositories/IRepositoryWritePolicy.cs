namespace UltraSol.Shared.Infrastructure.Repositories;

/// <summary>Optional module restriction for operations that bypass aggregate behavior.</summary>
public interface IRepositoryWritePolicy
{
    void EnsureDeleteAllowed(Type entityType);
    void EnsureBulkWriteAllowed(Type entityType);
}