using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Modules.Pricing.Infrastructure.Persistence;
using UltraSol.Modules.Pricing.Infrastructure.Repositories;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Repositories;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace UltraSol.Modules.Pricing.Tests;

public sealed class PricingDatabaseFixture : IAsyncLifetime
{
    private readonly string _database = "ultrasol_pricing_test_" + Guid.NewGuid().ToString("N");
    private string? _adminConnection;
    private bool _created;
    public string Connection { get; private set; } = "";

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_PRICING_POSTGRES_TESTS") != "1")
        {
            return;
        }
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ULTRASOL_PRICING_TEST_CONNECTION") ?? ConfiguredConnection())
        {
            Pooling = false,
            Database = "postgres"
        };
        _adminConnection = builder.ConnectionString;
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        _created = true;
        builder.Database = _database;
        Connection = builder.ConnectionString;
        try
        {
            await using var db = Create();
            await db.Database.MigrateAsync();
            await db.Database.MigrateAsync();
            await using var catalog = Catalog();
            await catalog.Database.MigrateAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public PricingDbContext Create() => new(new DbContextOptionsBuilder<PricingDbContext>()
        .UseNpgsql(Connection, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "pricing")).Options);

    public CatalogDbContext Catalog() => new(new DbContextOptionsBuilder<CatalogDbContext>()
        .UseNpgsql(Connection, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "catalog")).Options);

    public static UltraSol.Modules.Pricing.Domain.Repositories.IPricingUnitOfWork Unit(PricingDbContext db) => new PricingUnitOfWork(db, new PricingTestDispatcher());

    internal static string ConfiguredConnection()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UltraSol.slnx")))
        {
            directory = directory.Parent;
        }
        if (directory is null)
        {
            throw new InvalidOperationException("Set ULTRASOL_PRICING_TEST_CONNECTION outside the repository.");
        }
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "src/Bootstrappers/UltraSol.Bootstrappers/appsettings.json")));
        return document.RootElement.GetProperty("Postgres").GetProperty("ConnectionString").GetString()!;
    }

    public async Task DisposeAsync()
    {
        if (!_created)
        {
            return;
        }
        const string prefix = "ultrasol_pricing_test_";
        if (!_database.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(_database[prefix.Length..], "N", out _))
        {
            throw new InvalidOperationException("Refusing to drop a database not owned by this fixture.");
        }
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{_database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
        _created = false;
    }
}

internal sealed class PricingTestDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class PricingPostgresFactAttribute : FactAttribute
{
    public PricingPostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_PRICING_POSTGRES_TESTS") != "1")
        {
            Skip = "Enable ULTRASOL_PRICING_POSTGRES_TESTS to run against a disposable PostgreSQL database.";
        }
    }
}