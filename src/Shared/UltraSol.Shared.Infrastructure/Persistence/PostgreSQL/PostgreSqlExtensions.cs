using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;

public static class PostgreSqlExtensions
{
    public static IServiceCollection AddPostgres(this IServiceCollection services)
    {
        var postgreSqlOptions = services.GetOptions<PostgreSqlOptions>(PostgreSqlOptions.SectionName);
        if (string.IsNullOrWhiteSpace(postgreSqlOptions.ConnectionString))
        {
            throw new InvalidOperationException("Postgres:ConnectionString is required.");
        }
        services.AddSingleton(postgreSqlOptions);
        return services;
    }
    public static IServiceCollection AddPostgres<TContext>(this IServiceCollection services, bool runMigration = false)
        where TContext : DbContext
    {
        var postgreSqlOptions = services.GetOptions<PostgreSqlOptions>(PostgreSqlOptions.SectionName);
        services.AddDbContext<TContext>(options=>
        {
            options.UseNpgsql(postgreSqlOptions.ConnectionString, postgres =>
            {
                postgres.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
            });
        });
        if (runMigration)
        {
            //using var scope = services.BuildServiceProvider().CreateScope();
            //var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
            //dbContext.Database.Migrate();
        }
        return services;
    }
}