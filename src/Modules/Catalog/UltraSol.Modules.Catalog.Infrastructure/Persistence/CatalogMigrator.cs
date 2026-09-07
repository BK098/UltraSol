using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence;

public static class CatalogMigrator
{
    public static async Task MigrateAsync(CatalogDbContext context, CancellationToken cancellationToken = default)
    {
        var creator = context.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync(cancellationToken))
            await creator.CreateAsync(cancellationToken);
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'catalog'),
                       to_regclass('catalog."__EFMigrationsHistory"') IS NOT NULL
                """;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                await reader.ReadAsync(cancellationToken);
                if (reader.GetBoolean(0) && !reader.GetBoolean(1))
                {
                    throw new InvalidOperationException("Catalog schema contains unmanaged tables; migration stopped without resetting data.");
                }
            }
            await context.Database.MigrateAsync(cancellationToken);
        }
        finally 
        { 
            await context.Database.CloseConnectionAsync(); 
        }
    }
}