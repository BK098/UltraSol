using Microsoft.EntityFrameworkCore;
using UltraSol.Modules.Catalog.Infrastructure.Repositories;
using UltraSol.Shared.Domain.Common.Abstractions;
using UltraSol.Shared.Domain.Common.Events;
using UltraSol.Shared.Domain.Common.Repositories;
using UltraSol.Shared.Infrastructure.Repositories;
using Xunit;

namespace UltraSol.Modules.Catalog.Infrastructure.Tests;

public class UnitOfWorkTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    // A carrier stays Unchanged: transactions commit no database rows.
    private sealed class Carrier : IHasDomainEvents
    {
        public int Id { get; set; }
        private readonly List<IDomainEvent> _events = [];
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _events.AsReadOnly();
        public void AddDomainEvent(IDomainEvent domainEvent) => _events.Add(domainEvent);
        public void RemoveDomainEvent(IDomainEvent domainEvent) => _events.Remove(domainEvent);
        public void ClearDomainEvents() => _events.Clear();
    }
    private sealed class EventContext(DbContextOptions<EventContext> options) : DbContext(options)
    {
        public bool FailSave { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Carrier>().HasKey(x => x.Id);
            modelBuilder.Entity<Carrier>().Ignore(x => x.DomainEvents);
        }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            FailSave ? Task.FromException<int>(new InvalidOperationException("Test save failure")) : base.SaveChangesAsync(cancellationToken);
    }
    private sealed class Dispatcher : IDomainEventDispatcher
    {
        public int Count { get; private set; }
        public bool Fail { get; set; }
        public Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new InvalidOperationException("Test dispatch failure");
            Count += events.Count();
            return Task.CompletedTask;
        }
    }
    private sealed record Event : DomainEvent;
    private EventContext Create() => new(new DbContextOptionsBuilder<EventContext>().UseNpgsql(fixture.Connection).Options);

    [Fact]
    public async Task EventsWaitUntilCommitAndRollbackKeepsEventsOnOwner()
    {
        await using var db = Create(); var dispatcher = new Dispatcher();
        var uow = new CatalogUnitOfWork(db, dispatcher);
        var carrier = new Carrier { Id = 1 }; carrier.AddDomainEvent(new Event()); db.Attach(carrier);
        await using (var transaction = await uow.BeginTransactionAsync())
        {
            await uow.SaveChangesAsync();
            Assert.Equal(0, dispatcher.Count); Assert.Single(carrier.DomainEvents);
            await transaction.CommitAsync();
        }
        Assert.Equal(1, dispatcher.Count); Assert.Empty(carrier.DomainEvents);
        carrier.AddDomainEvent(new Event());
        await using (var transaction = await uow.BeginTransactionAsync())
        {
            await uow.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        Assert.Single(carrier.DomainEvents); Assert.Equal(1, dispatcher.Count);
    }

    [Fact]
    public async Task SaveFailureDoesNotLoseEventsOrDispatch()
    {
        await using var db = Create(); 
        var dispatcher = new Dispatcher(); 
        var uow = new CatalogUnitOfWork(db, dispatcher);
        var carrier = new Carrier { Id = 2 }; carrier.AddDomainEvent(new Event()); db.Attach(carrier);
        db.FailSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.SaveChangesAsync());
        Assert.Single(carrier.DomainEvents); Assert.Equal(0, dispatcher.Count);
    }

    [Fact]
    public async Task HandlerFailureDoesNotReplayCommittedWork()
    {
        await using var db = Create(); 
        var dispatcher = new Dispatcher { Fail = true };
        var uow = new CatalogUnitOfWork(db, dispatcher);
        var carrier = new Carrier { Id = 3 }; carrier.AddDomainEvent(new Event()); db.Attach(carrier);
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(_ => { calls++; return Task.CompletedTask; }));
        Assert.Equal(1, calls); Assert.Single(carrier.DomainEvents);
    }
}