using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;

public static class PostgreSqlExtensions
{
    public static IServiceCollection AddPostgres(this IServiceCollection services)
    {
        var postgreSqlOptions = services.GetOptions<PostgreSqlOptions>(PostgreSqlOptions.SectionName);
        services.AddSingleton(postgreSqlOptions);
        if (string.IsNullOrWhiteSpace(postgreSqlOptions.ConnectionString))
        {
            throw new InvalidOperationException("Postgres:ConnectionString is required.");
        }
        services.AddHostedService<PostgreSqlConnectionChecker>();
        services.AddHostedService<PostgreSqlConnectionMonitor>();
        return services;
    }
    public static IServiceCollection AddPostgres<TContext>(this IServiceCollection services, string schemaName)
        where TContext : DbContext
    {
        var postgreSqlOptions = services.GetOptions<PostgreSqlOptions>(PostgreSqlOptions.SectionName);
        services.AddDbContext<TContext>(options =>
        {
            options.UseNpgsql(postgreSqlOptions.ConnectionString, postgres =>
            {
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
                postgres.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            });
        });
        return services;
    }
    public static async Task MigrateAsync<TContext>(this IServiceProvider services, CancellationToken cancellationToken = default)
    where TContext : DbContext
    {
        await using var scope = services.CreateAsyncScope();

        var context =
            scope.ServiceProvider.GetRequiredService<TContext>();

        await context.Database
            .MigrateAsync(cancellationToken);
    }
}