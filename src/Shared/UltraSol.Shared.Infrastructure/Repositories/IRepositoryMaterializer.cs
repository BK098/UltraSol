namespace UltraSol.Shared.Infrastructure.Repositories;

/// <summary>Optional DbContext hook for assembling an aggregate after repository materialization.</summary>
public interface IRepositoryMaterializer
{
    Task MaterializeAsync(object entity, CancellationToken cancellationToken = default);
}

internal static class RepositoryMaterialization
{
    internal static async Task<T?> OneAsync<T>(this Microsoft.EntityFrameworkCore.DbContext context,
        Task<T?> query, CancellationToken ct) where T : class
    {
        var entity = await query.ConfigureAwait(false);
        if (entity is not null && context is IRepositoryMaterializer loader)
            await loader.MaterializeAsync(entity, ct).ConfigureAwait(false);
        return entity;
    }

    internal static async Task<List<T>> ManyAsync<T>(this Microsoft.EntityFrameworkCore.DbContext context,
        Task<List<T>> query, CancellationToken ct) where T : class
    {
        var entities = await query.ConfigureAwait(false);
        if (context is IRepositoryMaterializer loader)
            foreach (var entity in entities) await loader.MaterializeAsync(entity, ct).ConfigureAwait(false);
        return entities;
    }
}

