using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record PublishCollectionDto(string Name);
public sealed record PublishCollectionCommand(PublishCollectionDto Model) : ICommand<ApiResult<object>>;
public sealed class PublishCollectionValidator : AbstractValidator<PublishCollectionCommand>
{
    public PublishCollectionValidator()
    {
    }
}
internal sealed class PublishCollectionCommandHandler : ICommandHandler<PublishCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(PublishCollectionCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}