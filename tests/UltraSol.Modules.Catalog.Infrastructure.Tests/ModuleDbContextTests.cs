using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UltraSol.Modules.Catalog.Infrastructure.Persistence;
using UltraSol.Shared.Domain.Common.Entities;
using UltraSol.Shared.Infrastructure.Persistence;
using Xunit;

namespace UltraSol.Modules.Catalog.Infrastructure.Tests;

public class ModuleDbContextTests
{
    private sealed class SampleAggregate : AggregateRoot
    {
        public string Name { get; set; } = "Sample";
    }

    // Suppress only the database write; the module pipeline and EF tracking still execute.
    private sealed class SaveObserver : SaveChangesInterceptor
    {
        public int Calls { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(7));
        }
    }

    private sealed class SampleContext(DbContextOptions<SampleContext> options) : ModuleDbContext<SampleContext>(options)
    {
        public bool TransactionRequired { get; set; }
        public bool MaterializationRequired { get; set; }
        public bool DeletionAllowed { get; set; } = true;
        public bool FailHydration { get; set; }
        public int Hydrations { get; private set; }
        public int Preparations { get; private set; }
        protected override bool RequireTransaction => TransactionRequired;
        protected override bool RequireMaterializedAggregates => MaterializationRequired;
        protected override bool AllowAggregateDeletion => DeletionAllowed;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SampleAggregate>().HasKey(root => root.Id);
            modelBuilder.Entity<SampleAggregate>().Ignore(root => root.DomainEvents);
        }

        protected override Task HydrateAggregateAsync(AggregateRoot aggregate, CancellationToken cancellationToken)
        {
            Hydrations++;
            if (FailHydration)
            {
                throw new InvalidOperationException("Hydration failed.");
            }
            return Task.CompletedTask;
        }

        protected override Task PrepareAggregatesAsync(IReadOnlyList<AggregateRoot> aggregates, CancellationToken cancellationToken)
        {
            Preparations++;
            return Task.CompletedTask;
        }
    }

    private static SampleContext Create(SaveObserver observer) => new(new DbContextOptionsBuilder<SampleContext>()
        .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
        .AddInterceptors(observer).Options);

    [Fact]
    public async Task AddedAggregateCanSaveThroughEitherAsyncOverload()
    {
        var observer = new SaveObserver();
        await using var db = Create(observer);
        db.MaterializationRequired = true;
        var root = new SampleAggregate();
        db.Add(root);
        Assert.Equal(7, await db.SaveChangesAsync());
        Assert.Equal(7, await db.SaveChangesAsync(true));
        Assert.Equal(2, db.Preparations);
        Assert.Equal(2, observer.Calls);
        Assert.Equal(0, db.Hydrations);
    }

    [Fact]
    public async Task IncompleteTrackedAggregateMustBeMaterializedBeforeSaving()
    {
        var observer = new SaveObserver();
        await using var db = Create(observer);
        db.MaterializationRequired = true;
        var root = new SampleAggregate();
        db.Attach(root);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal(0, db.Preparations);
        Assert.Equal(0, observer.Calls);
        await db.MaterializeAsync(root);
        await db.MaterializeAsync(root);
        Assert.Equal(1, db.Hydrations);
        Assert.Equal(7, await db.SaveChangesAsync());
    }

    [Fact]
    public async Task DetachedReadsDoNotAuthorizeAnAttachedAggregateForSaving()
    {
        await using var db = Create(new SaveObserver());
        db.MaterializationRequired = true;
        var root = new SampleAggregate();
        await db.MaterializeAsync(root);
        await db.MaterializeAsync(root);
        Assert.Equal(2, db.Hydrations);
        db.Attach(root);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task FailedHydrationCanRetryAndDoesNotAuthorizeSaving()
    {
        await using var db = Create(new SaveObserver());
        db.MaterializationRequired = true;
        db.FailHydration = true;
        var root = new SampleAggregate();
        db.Attach(root);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.MaterializeAsync(root));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.FailHydration = false;
        await db.MaterializeAsync(root);
        Assert.Equal(7, await db.SaveChangesAsync());
        Assert.Equal(2, db.Hydrations);
    }

    [Fact]
    public async Task ContextsKeepPoliciesAndMaterializationIndependent()
    {
        await using var permissive = Create(new SaveObserver());
        await using var strict = Create(new SaveObserver());
        strict.MaterializationRequired = true;
        strict.DeletionAllowed = false;
        var root = new SampleAggregate();
        permissive.Attach(root);
        strict.Attach(root);
        await permissive.MaterializeAsync(root);
        await Assert.ThrowsAsync<InvalidOperationException>(() => strict.SaveChangesAsync());
        await strict.MaterializeAsync(root);
        strict.Remove(root);
        await Assert.ThrowsAsync<InvalidOperationException>(() => strict.SaveChangesAsync());
        permissive.Remove(root);
        Assert.Equal(7, await permissive.SaveChangesAsync());
    }

    [Fact]
    public async Task TransactionPolicyRejectsSaveBeforePreparation()
    {
        var observer = new SaveObserver();
        await using var db = Create(observer);
        db.TransactionRequired = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal(0, db.Preparations);
        Assert.Equal(0, observer.Calls);
    }

    [Fact]
    public async Task UnsupportedSaveModesAndCancellationDoNotReachPreparation()
    {
        var observer = new SaveObserver();
        await using var db = Create(observer);
        Assert.Throws<NotSupportedException>(() => db.SaveChanges());
        Assert.Throws<NotSupportedException>(() => db.SaveChanges(true));
        await Assert.ThrowsAsync<NotSupportedException>(() => db.SaveChangesAsync(false));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.SaveChangesAsync(cancellation.Token));
        Assert.Equal(0, db.Preparations);
        Assert.Equal(0, observer.Calls);
    }

    [Fact]
    public async Task CatalogStillRequiresTransactionAndRejectsSynchronousSave()
    {
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Throws<NotSupportedException>(() => db.SaveChanges());
    }
}