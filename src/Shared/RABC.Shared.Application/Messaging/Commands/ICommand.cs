using MediatR;

namespace UltraSol.Shared.Application.Messaging.Commands;

public interface ICommand: IRequest
{
}

public interface ICommand<out TResponse> : IRequest<TResponse>
{
}