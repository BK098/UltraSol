using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UltraSol.Shared.Infrastructure.Persistence.PostgreSQL
{
    public static class PostgreSqlExtensions
    {
        public static IServiceCollection AddPostgres<TContext>(this IServiceCollection services, IConfiguration configuration)
            where TContext : DbContext
        {
            services.Configure<PostgreSqlOptions>(configuration.GetSection(PostgreSqlOptions.SectionName));
            services.AddDbContext<TContext>((serviceProvider, options) =>
            {
                var postgresOptions = serviceProvider.GetRequiredService<IOptions<PostgreSqlOptions>>().Value;
                if (string.IsNullOrWhiteSpace(postgresOptions.ConnectionString))
                {
                    throw new ArgumentException("Postgres connection string is not configured.");
                }

                var retryDelay = TimeSpan.FromSeconds(30);//seconds
                var maxRetry = 5;
                options.UseNpgsql(
                    postgresOptions.ConnectionString,
                    sqlOptions =>
                        sqlOptions.EnableRetryOnFailure(maxRetry, retryDelay, null));
            });
            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
            dbContext.Database.Migrate();
            return services;
        }
    }
}