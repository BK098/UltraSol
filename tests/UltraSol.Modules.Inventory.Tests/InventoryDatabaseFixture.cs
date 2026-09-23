using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UltraSol.Modules.Inventory.Api;
using UltraSol.Modules.Inventory.Infrastructure;
using UltraSol.Modules.Inventory.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Repositories;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace UltraSol.Modules.Inventory.Tests;

public sealed class InventoryDatabaseFixture : IAsyncLifetime
{
    private readonly string _database = "ultrasol_inventory_test_" + Guid.NewGuid().ToString("N");
    private string? _adminConnection;
    private bool _created;
    public string Connection { get; private set; } = "";

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_INVENTORY_POSTGRES_TESTS") != "1")
        {
            return;
        }
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ULTRASOL_INVENTORY_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set ULTRASOL_INVENTORY_TEST_CONNECTION for disposable database tests.")) { Pooling = false };
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
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public InventoryDbContext Create() => new(new DbContextOptionsBuilder<InventoryDbContext>()
        .UseNpgsql(Connection, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "inventory")).Options);

    public ServiceProvider Services(TimeProvider? clock = null, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Postgres:ConnectionString"] = Connection }).Build();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<TimeProvider>(clock ?? TimeProvider.System);
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddInventoryModule(configuration);
        configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public async Task DisposeAsync()
    {
        if (!_created)
        {
            return;
        }
        if (!_database.StartsWith("ultrasol_inventory_test_", StringComparison.Ordinal) || !Guid.TryParseExact(_database["ultrasol_inventory_test_".Length..], "N", out _))
        {
            throw new InvalidOperationException("Refusing to drop a database not created by this fixture.");
        }
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        _created = false;
    }
}

public sealed class InventoryPostgresFactAttribute : FactAttribute
{
    public InventoryPostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_INVENTORY_POSTGRES_TESTS") != "1")
        {
            Skip = "Enable ULTRASOL_INVENTORY_POSTGRES_TESTS for disposable PostgreSQL tests.";
        }
    }
}

public sealed class InventoryTestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}