using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record UnpublishCollectionDto(string Name);
public sealed record UnpublishCollectionCommand(UnpublishCollectionDto Model) : ICommand<ApiResult<object>>;
public sealed class UnpublishCollectionValidator : AbstractValidator<UnpublishCollectionCommand>
{
    public UnpublishCollectionValidator()
    {
    }
}
internal sealed class UnpublishCollectionCommandHandler : ICommandHandler<UnpublishCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(UnpublishCollectionCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}