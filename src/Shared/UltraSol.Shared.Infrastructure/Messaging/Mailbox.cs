using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltraSol.Shared.Application.Messaging.Integration;
using UltraSol.Shared.Domain.Common.Repositories;
namespace UltraSol.Shared.Infrastructure.Messaging;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
}
public sealed class InboxMessage
{
    public Guid Id { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
public static class MailboxMapping
{
    public static void MapMailbox(this ModelBuilder builder)
    {
        builder.Entity<OutboxMessage>().ToTable("outbox").HasKey(x => x.Id);
        builder.Entity<OutboxMessage>().Property(x => x.Type).HasMaxLength(256);
        builder.Entity<OutboxMessage>().HasIndex(x => new { x.PublishedAt, x.CreatedAt });
        builder.Entity<InboxMessage>().ToTable("inbox").HasKey(x => x.Id);
    }
}
public interface IMailbox
{
    Task<OutboxMessage?> NextAsync(CancellationToken ct);
    Task MarkPublishedAsync(Guid id, CancellationToken ct);
    Task ReceiveAsync(Type contract, string payload, CancellationToken ct);
}
public sealed class Mailbox<TContext>(TContext db, IUnitOfWork unit, IServiceProvider services) : IMailbox, IOutbox
    where TContext : DbContext
{
    public void Add<T>(T message)
    {
        var payload = JsonSerializer.Serialize(message);
        using var json = JsonDocument.Parse(payload);
        var id = json.RootElement.GetProperty("EventId").GetGuid();
        db.Set<OutboxMessage>().Add(new OutboxMessage { Id = id, Type = typeof(T).FullName!, Payload = payload });
    }
    public Task<OutboxMessage?> NextAsync(CancellationToken ct) => db.Set<OutboxMessage>().AsNoTracking().Where(x => x.PublishedAt == null).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(ct);
    public Task MarkPublishedAsync(Guid id, CancellationToken ct) => unit.ExecuteInTransactionAsync(async token =>
    {
        var message = await db.Set<OutboxMessage>().SingleAsync(x => x.Id == id, token);
        message.PublishedAt = DateTimeOffset.UtcNow;
    }, ct);
    public Task ReceiveAsync(Type contract, string payload, CancellationToken ct) => unit.ExecuteInTransactionAsync(async token =>
    {
        // ponytail: serialize each module's inbox; partition locks if consumer throughput becomes limiting.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({typeof(TContext).FullName!}))", token);
        using var json = JsonDocument.Parse(payload);
        var id = json.RootElement.GetProperty("EventId").GetGuid();
        if (json.RootElement.GetProperty("ContractVersion").GetInt32() != 1)
        {
            throw new InvalidOperationException("Unsupported integration contract version.");
        }
        if (await db.Set<InboxMessage>().AnyAsync(x => x.Id == id, token))
        {
            return;
        }
        var message = JsonSerializer.Deserialize(payload, contract) ?? throw new InvalidOperationException("Missing integration payload.");
        var handlerType = typeof(IIntegrationHandler<>).MakeGenericType(contract);
        var handler = services.GetRequiredService(handlerType);
        await (Task)handlerType.GetMethod("HandleAsync")!.Invoke(handler, [message, token])!;
        db.Set<InboxMessage>().Add(new InboxMessage { Id = id });
    }, ct);
}