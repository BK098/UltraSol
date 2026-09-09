using FluentValidation;
using UltraSol.Shared.Application.Messaging.Commands;
using UltraSol.Shared.Application.Responses;

namespace UltraSol.Modules.Catalog.Application.Features.Collections.Commands;

public sealed record CreateCollectionDto(string Name);
public sealed record CreateCollectionCommand(CreateCollectionDto Model) : ICommand<ApiResult<object>>;
public sealed class CreateCollectionValidator : AbstractValidator<CreateCollectionCommand>
{
    public CreateCollectionValidator()
    {
    }
}
internal sealed class CreateCollectionCommandHandler : ICommandHandler<CreateCollectionCommand, ApiResult<object>>
{
    public Task<ApiResult<object>> Handle(CreateCollectionCommand request, CancellationToken cancellationToken)
    {
        var model = request.Model;
        throw new NotImplementedException();
    }
}