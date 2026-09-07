using MediatR;

namespace UltraSol.Shared.Application.Messaging.Quries;

public interface IQuery<out TResponse> : IRequest<TResponse>
{
}