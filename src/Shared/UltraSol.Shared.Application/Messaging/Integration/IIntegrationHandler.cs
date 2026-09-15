namespace UltraSol.Shared.Application.Messaging.Integration;
public interface IIntegrationHandler<T>
{
    Task HandleAsync(T message, CancellationToken ct);
}
public interface IOutbox
{
    void Add<T>(T message);
}