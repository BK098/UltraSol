namespace UltraSol.Shared.Infrastructure.Messaging;

public sealed record ModuleMailbox(string Name, Func<IServiceProvider, IMailbox> Resolve, Type[] Subscriptions);
