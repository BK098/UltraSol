using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UltraSol.Shared.Infrastructure.ThirdParties.MessageQueues.RabbitMessageQueues;

namespace UltraSol.Shared.Infrastructure.Messaging;

public sealed class OutboxPublisher(IServiceScopeFactory scopes, IEnumerable<ModuleMailbox> configured, IBus bus, RabbitOptions options, ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var modules = configured.ToArray();
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var module in modules)
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var mailbox = module.Resolve(scope.ServiceProvider);
                    var message = await mailbox.NextAsync(stoppingToken);
                    if (message is null)
                    {
                        continue;
                    }
                    var recipients = modules.Where(x => x.Subscriptions.Any(t => t.FullName == message.Type)).ToArray();
                    if (recipients.Length == 0)
                    {
                        throw new InvalidOperationException("No subscriber registered for " + message.Type);
                    }
                    var contract = recipients[0].Subscriptions.Single(t => t.FullName == message.Type);
                    var payload = JsonSerializer.Deserialize(message.Payload, contract)!;
                    using var json = JsonDocument.Parse(message.Payload);
                    foreach (var recipient in recipients)
                    {
                        // Durable queues accept events before consumers start; inbox deduplicates partial fan-out retries.
                        var endpoint = await bus.GetSendEndpoint(new Uri("queue:" + options.Prefix + "." + recipient.Name));
                        await endpoint.Send(payload, contract, context =>
                        {
                            context.MessageId = json.RootElement.GetProperty("EventId").GetGuid();
                            context.CorrelationId = json.RootElement.GetProperty("CorrelationId").GetGuid();
                        }, stoppingToken);
                    }
                    await mailbox.MarkPublishedAsync(message.Id, stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning("Outbox {Module} retained for retry ({ErrorType}).", module.Name, exception.GetType().Name);
                }
            }
            // ponytail: one row per module per poll; batch when outbox throughput requires it.
            await Task.Delay(500, stoppingToken);
        }
    }
}
