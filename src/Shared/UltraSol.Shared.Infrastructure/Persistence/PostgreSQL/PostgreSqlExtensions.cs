using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace UltraSol.Shared.Infrastructure.Persistence.PostgreSQL;

public static class PostgreSqlExtensions
{
    public static IServiceCollection AddPostgres<TContext>(this IServiceCollection services,
        IConfiguration configuration, Action<NpgsqlDbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        services.Configure<PostgreSqlOptions>(configuration.GetSection(PostgreSqlOptions.SectionName));
        services.AddDbContext<TContext>((provider, options) =>
        {
            var settings = provider.GetRequiredService<IOptions<PostgreSqlOptions>>().Value;
            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new InvalidOperationException("Postgres:ConnectionString is required.");
            }
            options.UseNpgsql(settings.ConnectionString, postgres =>
            {
                postgres.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                configure?.Invoke(postgres);
            });
        });
        return services;
    }
}