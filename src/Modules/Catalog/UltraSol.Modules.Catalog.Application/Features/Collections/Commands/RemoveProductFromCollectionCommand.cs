using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record RemoveProductFromCollectionDto(string Name);
public sealed record RemoveProductFromCollectionCommand(RemoveProductFromCollectionDto Model) : ICommand<ApiResult<object>>;
public sealed class RemoveProductFromCollectionValidator : AbstractValidator<RemoveProductFromCollectionCommand>
{
    public RemoveProductFromCollectionValidator()
    {
    }
}
internal sealed class RemoveProductFromCollectionCommandHandler : ICommandHandler<RemoveProductFromCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(RemoveProductFromCollectionCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}