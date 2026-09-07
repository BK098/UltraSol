using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace UltraSol.Modules.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var path = FindBootstrapper();
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var builder = new ConfigurationBuilder().SetBasePath(path)
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment}.json", optional: true);
        if (environment == "Development")
            builder.AddUserSecrets("8acea3cd-4fae-45cd-824c-59ce0fb299a7");
        var config = builder.AddEnvironmentVariables().Build();
        var connection = config["Postgres:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Postgres:ConnectionString is required.");
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connection, postgres =>
            {
                postgres.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                CatalogPostgresConfiguration.Configure(postgres);
            });
        return new CatalogDbContext(options.Options);
    }

    private static string FindBootstrapper()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "src", "Bootstrappers", "UltraSol.Bootstrappers");
                if (File.Exists(Path.Combine(candidate, "appsettings.json"))) return candidate;
                if (directory.Name == "UltraSol.Bootstrappers" && File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
                {
                    return directory.FullName;
                }
            }
        }
        throw new DirectoryNotFoundException("Cannot locate Bootstrapper configuration.");
    }
}