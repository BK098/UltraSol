using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltraSol.Modules.Ordering.Infrastructure.Persistence;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace UltraSol.Modules.Ordering.Tests;

public sealed class OrderingDatabaseFixture : IAsyncLifetime
{
    private readonly string _database = "ultrasol_ordering_test_" + Guid.NewGuid().ToString("N");
    private string? _adminConnection;
    private string _connection = "";
    private bool _created;
    public string Connection => _connection;

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_ORDERING_POSTGRES_TESTS") != "1")
        {
            return;
        }
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ULTRASOL_ORDERING_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set ULTRASOL_ORDERING_TEST_CONNECTION for disposable database tests."))
        {
            Pooling = false
        };
        _adminConnection = builder.ConnectionString;
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        _created = true;
        builder.Database = _database;
        _connection = builder.ConnectionString;
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

    public OrderingDbContext Create() => new(new DbContextOptionsBuilder<OrderingDbContext>()
        .UseNpgsql(_connection, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "ordering"))
        .Options);

    public async Task DisposeAsync()
    {
        if (!_created)
        {
            return;
        }
        if (!_database.StartsWith("ultrasol_ordering_test_", StringComparison.Ordinal) ||
            !Guid.TryParseExact(_database["ultrasol_ordering_test_".Length..], "N", out _))
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

public sealed class OrderingPostgresFactAttribute : FactAttribute
{
    public OrderingPostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_ORDERING_POSTGRES_TESTS") != "1")
        {
            Skip = "Enable ULTRASOL_ORDERING_POSTGRES_TESTS for disposable PostgreSQL tests.";
        }
    }
}