using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence;

public static class CatalogPostgresConfiguration
{
    public static void Configure(NpgsqlDbContextOptionsBuilder options) =>
        options.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName)
            .MigrationsHistoryTable("__EFMigrationsHistory", Schema.Name);
}