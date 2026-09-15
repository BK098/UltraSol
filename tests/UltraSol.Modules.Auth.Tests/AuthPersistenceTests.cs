using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UltraSol.Modules.Auth.Domain.Abstractions;
using UltraSol.Modules.Auth.Domain.Accounts;
using UltraSol.Modules.Auth.Domain.Sessions;
using UltraSol.Modules.Auth.Infrastructure.Persistence;
using UltraSol.Modules.Auth.Infrastructure.Repositories;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Repositories;
using Xunit;

namespace UltraSol.Modules.Auth.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_AUTH_POSTGRES_TESTS") != "1")
        {
            Skip = "Set ULTRASOL_AUTH_POSTGRES_TESTS=1 to run against a disposable PostgreSQL database.";
        }
    }
}

public sealed class AuthDatabaseFixture : IAsyncLifetime
{
    private readonly string _database = "ultrasol_auth_test_" + Guid.NewGuid().ToString("N");
    private string? _adminConnection;
    private bool _created;
    public string Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("ULTRASOL_AUTH_POSTGRES_TESTS") != "1")
        {
            return;
        }
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ULTRASOL_AUTH_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set ULTRASOL_AUTH_TEST_CONNECTION for the disposable test database.")) { Pooling = false };
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
            await using var services = AuthModelTests.Services(Connection);
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (!_created)
        {
            return;
        }
        if (!_database.StartsWith("ultrasol_auth_test_", StringComparison.Ordinal) ||
            !Guid.TryParseExact(_database["ultrasol_auth_test_".Length..], "N", out _))
        {
            throw new InvalidOperationException("Refusing to drop a database not created by this test fixture.");
        }
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{_database}\"", admin);
        await command.ExecuteNonQueryAsync();
        _created = false;
    }
}

public sealed class AuthPersistenceTests(AuthDatabaseFixture database) : IClassFixture<AuthDatabaseFixture>
{
    private const string Password = "Auth-Example-482!";
    private static string Email() => $"{Guid.NewGuid():N}@example.com";

    private static void EnsureSuccess(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));

    [PostgresFact]
    public async Task MigrationMatchesModelAndCanBeAppliedTwice()
    {
        await using var services = AuthModelTests.Services(database.Connection);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await context.Database.MigrateAsync();
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(context.Database.GetMigrations(), await context.Database.GetAppliedMigrationsAsync());
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'auth'", connection);
        Assert.Equal(context.Model.GetRelationalModel().Tables.LongCount() + 1, await command.ExecuteScalarAsync());
    }

    [PostgresFact]
    public async Task IdentityPasswordAndTokensPersistThroughTheNativeStore()
    {
        var account = UserAccount.Create(Email(), DateTimeOffset.UtcNow);
        await using var services = AuthModelTests.Services(database.Connection);
        await using (var scope = services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();
            await scope.ServiceProvider.GetRequiredService<IAuthUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                EnsureSuccess(await manager.CreateAsync(account, Password));
                EnsureSuccess(await manager.SetAuthenticationTokenAsync(account, "test", "token", "stored-value"));
                EnsureSuccess(await manager.AddLoginAsync(account, new UserLoginInfo("test", Guid.NewGuid().ToString("N"), "Test")));
            });
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();
            var loaded = (await manager.FindByIdAsync(account.Id.ToString()))!;
            Assert.NotEqual(Password, loaded.PasswordHash);
            Assert.True(await manager.CheckPasswordAsync(loaded, Password));
            Assert.False(await manager.CheckPasswordAsync(loaded, "wrong-password"));
            Assert.Equal("stored-value", await manager.GetAuthenticationTokenAsync(loaded, "test", "token"));
            Assert.Single(await manager.GetLoginsAsync(loaded));
            var reset = await manager.GeneratePasswordResetTokenAsync(loaded);
            await scope.ServiceProvider.GetRequiredService<IAuthUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                EnsureSuccess(await manager.ResetPasswordAsync(loaded, reset, Password + "new"));
                EnsureSuccess(await manager.ResetAuthenticatorKeyAsync(loaded));
                EnsureSuccess(await manager.SetTwoFactorEnabledAsync(loaded, true));
            });
            Assert.True(await manager.CheckPasswordAsync(loaded, Password + "new"));
            Assert.False(await manager.CheckPasswordAsync(loaded, Password));
            Assert.NotNull(await manager.GetAuthenticatorKeyAsync(loaded));
            Assert.True(await manager.GetTwoFactorEnabledAsync(loaded));
        }
    }

    [PostgresFact]
    public async Task DeletedEmailCanRegisterANewIdentityWithoutOldSessionsOrTokens()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create(Email(), now);
        var session = Session.Create(account.Id, Device.Create("Browser", "Agent", "::1"), now, now.AddHours(1));
        await using var services = AuthModelTests.Services(database.Connection);
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var manager = provider.GetRequiredService<UserManager<UserAccount>>();
        var context = provider.GetRequiredService<AuthDbContext>();
        var unit = provider.GetRequiredService<IAuthUnitOfWork>();
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            EnsureSuccess(await manager.CreateAsync(account, Password));
            EnsureSuccess(await manager.SetAuthenticationTokenAsync(account, "test", "old", "old-value"));
            await provider.GetRequiredService<IRepository<Session>>().AddAsync(session, ct);
        });
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            var duplicate = UserAccount.Create(account.Email!.ToUpperInvariant(), now);
            var result = await manager.CreateAsync(duplicate, Password);
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Code == "DuplicateEmail");
        });
        var oldReset = await manager.GeneratePasswordResetTokenAsync(account);
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            account.SoftDelete(now.AddMinutes(1));
            session.Revoke("Account deleted", now.AddMinutes(1));
            EnsureSuccess(await manager.UpdateAsync(account));
        });
        context.ChangeTracker.Clear();
        Assert.Null(await manager.FindByEmailAsync(account.Email!));
        var replacement = UserAccount.Create(account.Email!.ToUpperInvariant(), now.AddMinutes(2));
        await unit.ExecuteInTransactionAsync(async ct => EnsureSuccess(await manager.CreateAsync(replacement, Password + "new")));
        Assert.NotEqual(account.Id, replacement.Id);
        Assert.Equal(2, await context.Users.IgnoreQueryFilters().CountAsync(user => user.NormalizedEmail == replacement.NormalizedEmail));
        Assert.Equal(replacement.Id, (await manager.FindByEmailAsync(account.Email!))!.Id);
        Assert.Empty(await context.Sessions.Where(value => value.UserId == replacement.Id).ToListAsync());
        Assert.Null(await manager.GetAuthenticationTokenAsync(replacement, "test", "old"));
        Assert.False(await manager.VerifyUserTokenAsync(replacement, manager.Options.Tokens.PasswordResetTokenProvider, "ResetPassword", oldReset));
        var oldSession = await context.Sessions.SingleAsync(value => value.Id == session.Id);
        Assert.Equal(session.Device, oldSession.Device);
        Assert.NotNull(oldSession.RevokedAt);
    }

    [PostgresFact]
    public async Task DatabaseUniqueIndexesRejectDuplicatesEvenWithoutUserManagerValidation()
    {
        await using var services = AuthModelTests.Services(database.Connection);
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var unit = scope.ServiceProvider.GetRequiredService<IAuthUnitOfWork>();
        var account = UserAccount.Create(Email(), DateTimeOffset.UtcNow);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();
        await unit.ExecuteInTransactionAsync(async ct => EnsureSuccess(await manager.CreateAsync(account, Password)));
        var duplicate = UserAccount.Create(account.Email!, DateTimeOffset.UtcNow);
        duplicate.NormalizedEmail = account.NormalizedEmail;
        duplicate.NormalizedUserName = account.NormalizedUserName;
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => unit.ExecuteInTransactionAsync(ct =>
        {
            context.Users.Add(duplicate);
            return Task.CompletedTask;
        }));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [PostgresFact]
    public async Task AccountAndSessionRollbackTogetherAndPublishOnlyAfterCommit()
    {
        var dispatcher = new RecordingDispatcher();
        await using var services = AuthModelTests.Services(database.Connection, dispatcher);
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var manager = provider.GetRequiredService<UserManager<UserAccount>>();
        var context = provider.GetRequiredService<AuthDbContext>();
        var unit = provider.GetRequiredService<IAuthUnitOfWork>();
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create(Email(), now);
        var session = Session.Create(account.Id, Device.Create("Browser"), now, now.AddHours(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteInTransactionAsync(async ct =>
        {
            EnsureSuccess(await manager.CreateAsync(account, Password));
            await provider.GetRequiredService<SessionRepository>().AddAsync(session, ct);
            account.Suspend(now);
            session.Revoke("Rollback", now);
            EnsureSuccess(await manager.UpdateAsync(account));
            Assert.Empty(dispatcher.Events);
            throw new InvalidOperationException("Abort command");
        }));
        Assert.Empty(dispatcher.Events);
        Assert.False(await context.Users.IgnoreQueryFilters().AnyAsync(user => user.Id == account.Id));
        Assert.False(await context.Sessions.AnyAsync(value => value.Id == session.Id));

        account = UserAccount.Create(Email(), now);
        session = Session.Create(account.Id, Device.Create("Browser"), now, now.AddHours(1));
        dispatcher.OnDispatch = async () =>
        {
            await using var readerScope = services.CreateAsyncScope();
            var reader = readerScope.ServiceProvider.GetRequiredService<AuthDbContext>();
            Assert.True(await reader.Users.AnyAsync(user => user.Id == account.Id && user.Status == AccountStatus.Suspended));
            Assert.True(await reader.Sessions.AnyAsync(value => value.Id == session.Id && value.RevokedAt != null));
        };
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            EnsureSuccess(await manager.CreateAsync(account, Password));
            await provider.GetRequiredService<SessionRepository>().AddAsync(session, ct);
            account.Suspend(now);
            session.Revoke("Commit", now);
            EnsureSuccess(await manager.UpdateAsync(account));
            Assert.Empty(dispatcher.Events);
        });
        Assert.Equal(2, dispatcher.Events.Count);
        Assert.Empty(account.DomainEvents);
        Assert.Empty(session.DomainEvents);
        Assert.True(await context.Users.AnyAsync(user => user.Id == account.Id && user.Status == AccountStatus.Suspended));
        Assert.True(await context.Sessions.AnyAsync(value => value.Id == session.Id && value.RevokedAt != null));
    }

    [PostgresFact]
    public async Task NativeIdentityAndSessionConcurrencyRejectStaleChanges()
    {
        await using var services = AuthModelTests.Services(database.Connection);
        await using var first = services.CreateAsyncScope();
        await using var second = services.CreateAsyncScope();
        var manager1 = first.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();
        var manager2 = second.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();
        var db1 = first.ServiceProvider.GetRequiredService<AuthDbContext>();
        var db2 = second.ServiceProvider.GetRequiredService<AuthDbContext>();
        var unit1 = first.ServiceProvider.GetRequiredService<IAuthUnitOfWork>();
        var unit2 = second.ServiceProvider.GetRequiredService<IAuthUnitOfWork>();
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create(Email(), now);
        var session = Session.Create(account.Id, Device.Create("Browser"), now, now.AddHours(1));
        await unit1.ExecuteInTransactionAsync(async ct =>
        {
            EnsureSuccess(await manager1.CreateAsync(account, Password));
            db1.Sessions.Add(session);
        });
        var staleAccount = (await manager2.FindByIdAsync(account.Id.ToString()))!;
        var staleSession = await db2.Sessions.SingleAsync(value => value.Id == session.Id);
        await unit1.ExecuteInTransactionAsync(async ct =>
        {
            account.Suspend(now.AddMinutes(1));
            session.Revoke("Logout", now.AddMinutes(1));
            EnsureSuccess(await manager1.UpdateAsync(account));
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => unit2.ExecuteInTransactionAsync(async ct =>
        {
            staleAccount.SoftDelete(now.AddMinutes(2));
            var result = await manager2.UpdateAsync(staleAccount);
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, error => error.Code == "ConcurrencyFailure");
            throw new InvalidOperationException("Reject failed IdentityResult");
        }));
        db2.Attach(staleSession);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => unit2.ExecuteInTransactionAsync(ct =>
        {
            staleSession.Touch(now.AddMinutes(2));
            return Task.CompletedTask;
        }));
        Assert.NotNull((await db2.Sessions.SingleAsync(value => value.Id == session.Id)).RevokedAt);
    }

    [PostgresFact]
    public async Task HardDeleteBulkWritesAndUnownedTransactionsAreRejected()
    {
        await using var services = AuthModelTests.Services(database.Connection);
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var context = provider.GetRequiredService<AuthDbContext>();
        var manager = provider.GetRequiredService<UserManager<UserAccount>>();
        var unit = provider.GetRequiredService<IAuthUnitOfWork>();
        var repository = provider.GetRequiredService<SessionRepository>();
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.Create(Email(), now);
        var session = Session.Create(account.Id, Device.Create("Browser"), now, now.AddHours(1));
        await unit.ExecuteInTransactionAsync(async ct =>
        {
            EnsureSuccess(await manager.CreateAsync(account, Password));
            await repository.AddAsync(session, ct);
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DeleteAsync(session));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DeleteWhereAsync(value => value.UserId == account.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ExecuteDeleteAsync(value => value.UserId == account.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ExecuteUpdateAsync(value => value.UserId == account.Id, update => update.Set(value => value.LastSeenAt, now)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteInTransactionAsync(async ct => await manager.DeleteAsync(account)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteInTransactionAsync(ct =>
        {
            context.Sessions.Remove(session);
            return Task.CompletedTask;
        }));
        await using var external = await context.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        await external.RollbackAsync();
    }

    private sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Events { get; } = [];
        public Func<Task>? OnDispatch { get; set; }

        public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            if (OnDispatch is not null)
            {
                await OnDispatch();
            }
            Events.AddRange(domainEvents);
        }
    }
}