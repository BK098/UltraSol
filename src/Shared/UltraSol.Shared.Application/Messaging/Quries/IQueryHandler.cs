using MediatR;

namespace UltraSol.Shared.Application.Messaging.Quries;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}