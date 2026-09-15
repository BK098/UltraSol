using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using UltraSol.Shared.Infrastructure.Messaging;

namespace UltraSol.Shared.Infrastructure.ThirdParties.MessageQueues.RabbitMessageQueues;

public static class RabbitExtensions
{
    public static IServiceCollection AddRabbitMessageQueues(this IServiceCollection services)
    {
        var options = services.GetOptions<RabbitOptions>(RabbitOptions.SectionName) ?? throw new InvalidOperationException("RabbitMQ configuration is required");
        services.AddSingleton(options);
        services.AddMassTransit(registration =>
        {
            registration.UsingRabbitMq((context, bus) =>
            {
                bus.Host(options.HostName, (ushort)options.Port, options.VirtualHost, host =>
                {
                    host.Username(options.UserName);
                    host.Password(options.Password);
                    host.PublisherConfirmation = true;
                });
                foreach (var module in context.GetServices<ModuleMailbox>())
                {
                    bus.ReceiveEndpoint(options.Prefix + "." + module.Name, endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Interval(4, TimeSpan.FromSeconds(5)));
                        foreach (var contract in module.Subscriptions)
                        {
                            typeof(RabbitExtensions).GetMethod(nameof(ConfigureHandler), BindingFlags.Static | BindingFlags.NonPublic)!
                                .MakeGenericMethod(contract).Invoke(null, [endpoint, context.GetRequiredService<IServiceScopeFactory>(), module]);
                        }
                    });
                }
            });
        });
        services.AddHostedService<OutboxPublisher>();
        return services;
    }

    private static void ConfigureHandler<T>(IRabbitMqReceiveEndpointConfigurator endpoint, IServiceScopeFactory scopes, ModuleMailbox module)
        where T : class
    {
        endpoint.Handler<T>(async context =>
        {
            await using var scope = scopes.CreateAsyncScope();
            await module.Resolve(scope.ServiceProvider).ReceiveAsync(typeof(T), JsonSerializer.Serialize(context.Message), context.CancellationToken);
        });
    }
}
